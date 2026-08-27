import { useState } from 'react';
import { CheckCircle2, Circle, Lock, Plus, X } from 'lucide-react';
import type { Task } from '@/types/task';
import { DEPENDENCY_TYPE_LABELS, type TaskDependency } from '@/types/dependency';
import {
  useCreateTaskDependency,
  useDeleteTaskDependency,
  useTaskDependencies,
  useTaskDependents,
} from '@/hooks/useTaskDependencies';
import { StatusBadge } from './StatusBadge';
import { PriorityBadge } from './PriorityBadge';
import { AddDependencyDialog } from './AddDependencyDialog';
import { Modal } from '@/components/shared/Modal';
import { ApiError } from '@/api/httpClient';
import { formatDate } from '@/utils/formatDate';
import '@/components/shared/ConfirmDeleteDialog.css';
import './DependenciesSection.css';

interface DependenciesSectionProps {
  task: Task;
  projectId: string;
  /** The full (unfiltered) project task list — reused for the Add Dependency selector rather
   * than a separate fetch, same "the caller already has this loaded" principle as GlobalSearch
   * reusing ?task=<id> instead of a new task-detail view. */
  tasks: Task[];
  /** Reuses the same mechanism ProjectPage already uses to open the panel (updates the `?task=`
   * param) — clicking a dependency swaps which task the already-open panel shows, rather than
   * navigating to a second detail view. */
  onOpenDetail: (taskId: string) => void;
  canEdit: boolean;
}

interface PendingRemoval {
  row: TaskDependency;
  targetTaskId: string;
  /** Direction only affects the arrow visual in the confirmation dialog. */
  direction: 'dependsOn' | 'blocking';
}

export function DependenciesSection({ task, projectId, tasks, onOpenDetail, canEdit }: DependenciesSectionProps) {
  const { data: dependsOn } = useTaskDependencies(task.id);
  const { data: blocking } = useTaskDependents(task.id);
  const createDependency = useCreateTaskDependency(task.id, projectId);
  const deleteDependency = useDeleteTaskDependency(projectId);
  const [showAddDialog, setShowAddDialog] = useState(false);
  const [pendingRemoval, setPendingRemoval] = useState<PendingRemoval | null>(null);

  const createErrorMessage = createDependency.isError
    ? createDependency.error instanceof ApiError
      ? createDependency.error.message
      : 'Could not add dependency.'
    : null;

  // What's actually blocking this task from STARTING right now — a subset of "Depends On" (an
  // unsatisfied Finish-to-Finish/Start-to-Finish row doesn't gate starting, only completing — see
  // TaskDependencyDto.isSatisfied's own doc comment). Shown explicitly rather than making the
  // reader infer it from each row's badge, per the spec's own "explain the reason, don't just say
  // Blocked" requirement.
  const startBlockers = (dependsOn ?? []).filter(
    (d) => !d.isSatisfied && (d.dependencyType === 'FinishToStart' || d.dependencyType === 'StartToStart'),
  );

  return (
    <div className="task-detail-panel__section">
      <div className="dependencies-section__header">
        <h3>Dependencies</h3>
        {canEdit && (
          <button type="button" className="dependencies-section__add" onClick={() => setShowAddDialog(true)}>
            <Plus size={13} />
            <span>Add Dependency</span>
          </button>
        )}
      </div>

      <div className="dependencies-section__counts">
        <span className={startBlockers.length > 0 ? 'dependencies-section__count--blocked' : undefined}>
          {startBlockers.length > 0 ? <Lock size={12} /> : null}
          Blocked by {startBlockers.length}
        </span>
        <span>Blocking {blocking?.length ?? 0}</span>
      </div>

      {startBlockers.length > 0 && (
        <div className="dependencies-section__blocked-banner">
          <Lock size={13} />
          <div>
            <strong>Blocked</strong>
            <p>
              Waiting for:{' '}
              {startBlockers.map((b, i) => (
                <span key={b.dependencyId}>
                  {i > 0 && ', '}
                  {b.title}
                </span>
              ))}
            </p>
          </div>
        </div>
      )}

      <div className="dependencies-section__group">
        <span className="dependencies-section__group-label">Depends On</span>
        {dependsOn?.length === 0 && (
          <p className="dependencies-section__empty">No dependencies. This task does not depend on another task.</p>
        )}
        {dependsOn?.map((row) => (
          <DependencyRow
            key={row.dependencyId}
            row={row}
            onOpen={() => onOpenDetail(row.taskId)}
            onRemove={canEdit ? () => setPendingRemoval({ row, targetTaskId: task.id, direction: 'dependsOn' }) : undefined}
          />
        ))}
      </div>

      <div className="dependencies-section__group">
        <span className="dependencies-section__group-label">Blocking</span>
        {blocking?.length === 0 && <p className="dependencies-section__empty">Not blocking any tasks.</p>}
        {blocking?.map((row) => (
          <DependencyRow
            key={row.dependencyId}
            row={row}
            onOpen={() => onOpenDetail(row.taskId)}
            onRemove={canEdit ? () => setPendingRemoval({ row, targetTaskId: row.taskId, direction: 'blocking' }) : undefined}
          />
        ))}
      </div>

      {showAddDialog && (
        <AddDependencyDialog
          task={task}
          tasks={tasks}
          existingDependsOnIds={new Set(dependsOn?.map((d) => d.taskId))}
          existingBlockingIds={new Set(blocking?.map((d) => d.taskId))}
          isPending={createDependency.isPending}
          errorMessage={createErrorMessage}
          onAdd={(dependsOnTaskId, dependencyType) =>
            createDependency.mutate(
              { dependsOnTaskId, dependencyType },
              { onSuccess: () => setShowAddDialog(false) },
            )
          }
          onClose={() => setShowAddDialog(false)}
        />
      )}

      {pendingRemoval && (
        <RemoveDependencyDialog
          task={task}
          pending={pendingRemoval}
          isPending={deleteDependency.isPending}
          onConfirm={() => {
            deleteDependency.mutate(
              { taskId: pendingRemoval.targetTaskId, dependencyId: pendingRemoval.row.dependencyId },
              { onSuccess: () => setPendingRemoval(null) },
            );
          }}
          onCancel={() => setPendingRemoval(null)}
        />
      )}
    </div>
  );
}

function DependencyRow({ row, onOpen, onRemove }: { row: TaskDependency; onOpen: () => void; onRemove?: () => void }) {
  return (
    <div className="dependency-row">
      <button type="button" className="dependency-row__main" onClick={onOpen}>
        <div className="dependency-row__title-line">
          <span className="dependency-row__satisfied" title={row.isSatisfied ? 'Satisfied' : 'Not yet satisfied'}>
            {row.isSatisfied ? <CheckCircle2 size={14} className="dependency-row__satisfied-icon" /> : <Circle size={14} />}
          </span>
          <span className="dependency-row__title">{row.title}</span>
          <span className="dependency-row__type">{DEPENDENCY_TYPE_LABELS[row.dependencyType]}</span>
        </div>
        <div className="dependency-row__meta">
          <StatusBadge status={row.status} />
          <PriorityBadge priority={row.priority} />
          {row.assignedTo && <span className="dependency-row__assignee">{row.assignedTo.name}</span>}
          {row.dueDate && <span className="dependency-row__due">Due {formatDate(row.dueDate)}</span>}
        </div>
      </button>
      {onRemove && (
        <button type="button" className="icon-button" aria-label={`Remove dependency on ${row.title}`} onClick={onRemove}>
          <X size={13} />
        </button>
      )}
    </div>
  );
}

function RemoveDependencyDialog({
  task,
  pending,
  isPending,
  onConfirm,
  onCancel,
}: {
  task: Task;
  pending: PendingRemoval;
  isPending: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}) {
  // The arrow always reads "prerequisite -> dependent", regardless of which list (Depends On vs
  // Blocking) the removal was triggered from — matches the spec's own mockup exactly.
  const [fromTitle, toTitle] = pending.direction === 'dependsOn' ? [pending.row.title, task.title] : [task.title, pending.row.title];

  return (
    <Modal onClose={onCancel}>
      <h2>Remove Dependency?</h2>
      <p className="confirm-delete-dialog__message">This will remove:</p>
      <div className="dependencies-section__remove-preview">
        <span>{fromTitle}</span>
        <span className="dependencies-section__remove-arrow">↓</span>
        <span>{toTitle}</span>
      </div>
      <div className="confirm-delete-dialog__actions">
        <button type="button" className="confirm-delete-dialog__cancel" onClick={onCancel} disabled={isPending}>
          Cancel
        </button>
        <button type="button" className="confirm-delete-dialog__delete" onClick={onConfirm} disabled={isPending}>
          {isPending ? 'Removing...' : 'Remove'}
        </button>
      </div>
    </Modal>
  );
}
