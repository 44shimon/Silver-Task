import { useState, type ChangeEvent } from 'react';
import { ChevronDown, Lock } from 'lucide-react';
import { STATUS_LABELS, STATUS_OPTIONS, type Task, type TaskStatus } from '@/types/task';
import { taskFieldChange, useUpdateTask } from '@/hooks/useTasks';
import { ApiError } from '@/api/httpClient';
import { OverrideDependencyDialog } from './OverrideDependencyDialog';
import './DropdownCell.css';

interface StatusDropdownCellProps {
  task: Task;
  projectId: string;
  /** Renders disabled (Phase 32 read-only mode, e.g. a Viewer) — the value still shows, it just
   * can't be changed. The backend independently rejects the write regardless; this only avoids
   * offering a control that would fail. */
  readOnly?: boolean;
  /** Phase 39 — whether the current viewer has Permissions.DependenciesOverride for this
   * project. Only affects whether the Override option is OFFERED when a change is blocked; the
   * backend independently re-checks the same permission on the actual override request. */
  canOverride?: boolean;
}

// Always rendered as a live <select> (no separate edit-mode toggle) — unlike free-text
// cells, picking an option is inherently a single atomic commit, so there's no draft
// state to manage and no risk of a stray click opening an editor with nothing to type.
export function StatusDropdownCell({ task, projectId, readOnly, canOverride }: StatusDropdownCellProps) {
  const updateTask = useUpdateTask(projectId);
  const [pendingStatus, setPendingStatus] = useState<TaskStatus | null>(null);
  const [showOverrideDialog, setShowOverrideDialog] = useState(false);

  const blockedBy = updateTask.error instanceof ApiError ? updateTask.error.errors?.blockedBy : undefined;
  const isDependencyBlocked = Boolean(blockedBy && blockedBy.length > 0);

  function handleChange(event: ChangeEvent<HTMLSelectElement>) {
    const newStatus = event.target.value as TaskStatus;
    if (newStatus !== task.status) {
      setPendingStatus(newStatus);
      updateTask.mutate({ task, change: taskFieldChange.status(newStatus) });
    }
  }

  function handleOverrideConfirm(reason: string) {
    if (!pendingStatus) {
      return;
    }
    updateTask.mutate(
      { task, change: taskFieldChange.statusOverride(pendingStatus, reason) },
      { onSuccess: () => setShowOverrideDialog(false) },
    );
  }

  return (
    <div className="dropdown-cell-wrapper">
      <select
        className={`dropdown-cell dropdown-cell--badge dropdown-cell--status-${task.status.toLowerCase()}${updateTask.isError ? ' dropdown-cell--error' : ''}`}
        value={task.status}
        onChange={handleChange}
        disabled={readOnly || updateTask.isPending}
        title={
          isDependencyBlocked
            ? `Blocked by: ${blockedBy!.join(', ')}`
            : updateTask.isError
              ? 'Could not save — try again'
              : undefined
        }
      >
        {STATUS_OPTIONS.map((status) => (
          <option key={status} value={status}>
            {STATUS_LABELS[status]}
          </option>
        ))}
      </select>
      <ChevronDown size={12} className="dropdown-cell__chevron" />

      {isDependencyBlocked && (
        <div className="dropdown-cell__dependency-error">
          <Lock size={11} />
          <span>Blocked by: {blockedBy!.join(', ')}</span>
          {canOverride && (
            <button type="button" onClick={() => setShowOverrideDialog(true)}>
              Override
            </button>
          )}
        </div>
      )}

      {showOverrideDialog && blockedBy && (
        <OverrideDependencyDialog
          blockedBy={blockedBy}
          isPending={updateTask.isPending}
          errorMessage={
            updateTask.isError && !isDependencyBlocked
              ? updateTask.error instanceof ApiError
                ? updateTask.error.message
                : 'Could not override.'
              : null
          }
          onConfirm={handleOverrideConfirm}
          onCancel={() => setShowOverrideDialog(false)}
        />
      )}
    </div>
  );
}
