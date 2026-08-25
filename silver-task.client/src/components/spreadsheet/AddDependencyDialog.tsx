import { useMemo, useState } from 'react';
import type { Task } from '@/types/task';
import { Modal } from '@/components/shared/Modal';
import { StatusBadge } from './StatusBadge';
import { PriorityBadge } from './PriorityBadge';
// Reuses ConfirmDeleteDialog's .confirm-delete-dialog__cancel button style for this dialog's
// own Close button, rather than redefining the same look under a new class name.
import '@/components/shared/ConfirmDeleteDialog.css';
import './AddDependencyDialog.css';

interface AddDependencyDialogProps {
  task: Task;
  tasks: Task[];
  existingDependsOnIds: Set<string>;
  existingBlockingIds: Set<string>;
  isPending: boolean;
  errorMessage: string | null;
  onAdd: (dependsOnTaskId: string) => void;
  onClose: () => void;
}

export function AddDependencyDialog({
  task,
  tasks,
  existingDependsOnIds,
  existingBlockingIds,
  isPending,
  errorMessage,
  onAdd,
  onClose,
}: AddDependencyDialogProps) {
  const [query, setQuery] = useState('');

  // Same-project + exclude-current-task + exclude-already-a-dependency are all authoritative
  // here (the full list). The circular-dependency exclusion is best-effort only: a task already
  // in "Blocking" (i.e. one that already depends on this task) would form an immediate one-hop
  // cycle if picked, so it's filtered out client-side — but a *transitive* cycle (through a third
  // task) isn't detectable without fetching the whole project's dependency graph here too, so
  // that case is still caught (and reported) by the backend's real cycle check on submit.
  const candidates = useMemo(() => {
    const normalized = query.trim().toLowerCase();
    return tasks.filter((candidate) => {
      if (candidate.id === task.id) {
        return false;
      }
      if (existingDependsOnIds.has(candidate.id) || existingBlockingIds.has(candidate.id)) {
        return false;
      }
      if (normalized && !candidate.title.toLowerCase().includes(normalized)) {
        return false;
      }
      return true;
    });
  }, [tasks, task.id, existingDependsOnIds, existingBlockingIds, query]);

  return (
    <Modal onClose={onClose} size="wide">
      <h2>Add Dependency</h2>
      <p className="add-dependency-dialog__subtitle">
        &ldquo;{task.title}&rdquo; will depend on the task you pick — it must be completed first.
      </p>

      <input
        type="text"
        className="add-dependency-dialog__search"
        placeholder="Search tasks..."
        value={query}
        onChange={(e) => setQuery(e.target.value)}
        autoFocus
      />

      <div className="add-dependency-dialog__list">
        {candidates.length === 0 && <p className="add-dependency-dialog__empty">No matching tasks.</p>}
        {candidates.map((candidate) => (
          <button
            key={candidate.id}
            type="button"
            className="add-dependency-dialog__row"
            disabled={isPending}
            onClick={() => onAdd(candidate.id)}
          >
            <span className="add-dependency-dialog__row-title">{candidate.title}</span>
            <div className="add-dependency-dialog__row-meta">
              <StatusBadge status={candidate.status} />
              <PriorityBadge priority={candidate.priority} />
              {candidate.assignedTo && <span className="add-dependency-dialog__row-assignee">{candidate.assignedTo.name}</span>}
            </div>
          </button>
        ))}
      </div>

      {errorMessage && <p className="form-error">{errorMessage}</p>}

      <div className="add-dependency-dialog__actions">
        <button type="button" className="confirm-delete-dialog__cancel" onClick={onClose}>
          Close
        </button>
      </div>
    </Modal>
  );
}
