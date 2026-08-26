import type { ChangeEvent } from 'react';
import { ChevronDown } from 'lucide-react';
import { STATUS_LABELS, STATUS_OPTIONS, type Task, type TaskStatus } from '@/types/task';
import { taskFieldChange, useUpdateTask } from '@/hooks/useTasks';
import './DropdownCell.css';

interface StatusDropdownCellProps {
  task: Task;
  projectId: string;
  /** Renders disabled (Phase 32 read-only mode, e.g. a Viewer) — the value still shows, it just
   * can't be changed. The backend independently rejects the write regardless; this only avoids
   * offering a control that would fail. */
  readOnly?: boolean;
}

// Always rendered as a live <select> (no separate edit-mode toggle) — unlike free-text
// cells, picking an option is inherently a single atomic commit, so there's no draft
// state to manage and no risk of a stray click opening an editor with nothing to type.
export function StatusDropdownCell({ task, projectId, readOnly }: StatusDropdownCellProps) {
  const updateTask = useUpdateTask(projectId);

  function handleChange(event: ChangeEvent<HTMLSelectElement>) {
    const newStatus = event.target.value as TaskStatus;
    if (newStatus !== task.status) {
      updateTask.mutate({ task, change: taskFieldChange.status(newStatus) });
    }
  }

  return (
    <div className="dropdown-cell-wrapper">
      <select
        className={`dropdown-cell dropdown-cell--badge dropdown-cell--status-${task.status.toLowerCase()}${updateTask.isError ? ' dropdown-cell--error' : ''}`}
        value={task.status}
        onChange={handleChange}
        disabled={readOnly || updateTask.isPending}
        title={updateTask.isError ? 'Could not save — try again' : undefined}
      >
        {STATUS_OPTIONS.map((status) => (
          <option key={status} value={status}>
            {STATUS_LABELS[status]}
          </option>
        ))}
      </select>
      <ChevronDown size={12} className="dropdown-cell__chevron" />
    </div>
  );
}
