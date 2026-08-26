import { useState } from 'react';
import { Plus, X } from 'lucide-react';
import type { Task } from '@/types/task';
import type { TaskDependency } from '@/types/dependency';
import {
  useCreateTaskDependency,
  useDeleteTaskDependency,
  useTaskDependencies,
  useTaskDependents,
} from '@/hooks/useTaskDependencies';
import { StatusBadge } from './StatusBadge';
import { PriorityBadge } from './PriorityBadge';
import { AddDependencyDialog } from './AddDependencyDialog';
import { ApiError } from '@/api/httpClient';
import { formatDate } from '@/utils/formatDate';
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

export function DependenciesSection({ task, projectId, tasks, onOpenDetail, canEdit }: DependenciesSectionProps) {
  const { data: dependsOn } = useTaskDependencies(task.id);
  const { data: blocking } = useTaskDependents(task.id);
  const createDependency = useCreateTaskDependency(task.id, projectId);
  const deleteDependency = useDeleteTaskDependency(projectId);
  const [showAddDialog, setShowAddDialog] = useState(false);

  function handleRemove(row: TaskDependency, targetTaskId: string) {
    // A dependency is cheap to undo (just re-add it), so a native confirm matches this app's
    // existing precedent for reversible-but-worth-a-pause deletes (e.g. AdminProjectsTable's
    // permanent-delete confirm) rather than a full custom dialog.
    if (!window.confirm(`Remove dependency on "${row.title}"?`)) {
      return;
    }
    deleteDependency.mutate({ taskId: targetTaskId, dependencyId: row.dependencyId });
  }

  const createErrorMessage = createDependency.isError
    ? createDependency.error instanceof ApiError
      ? createDependency.error.message
      : 'Could not add dependency.'
    : null;

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

      <div className="dependencies-section__group">
        <span className="dependencies-section__group-label">Depends On</span>
        {dependsOn?.length === 0 && <p className="dependencies-section__empty">No dependencies.</p>}
        {dependsOn?.map((row) => (
          <DependencyRow
            key={row.dependencyId}
            row={row}
            onOpen={() => onOpenDetail(row.taskId)}
            onRemove={canEdit ? () => handleRemove(row, task.id) : undefined}
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
            onRemove={canEdit ? () => handleRemove(row, row.taskId) : undefined}
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
          onAdd={(dependsOnTaskId) =>
            createDependency.mutate(dependsOnTaskId, { onSuccess: () => setShowAddDialog(false) })
          }
          onClose={() => setShowAddDialog(false)}
        />
      )}
    </div>
  );
}

function DependencyRow({ row, onOpen, onRemove }: { row: TaskDependency; onOpen: () => void; onRemove?: () => void }) {
  return (
    <div className="dependency-row">
      <button type="button" className="dependency-row__main" onClick={onOpen}>
        <span className="dependency-row__title">{row.title}</span>
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
