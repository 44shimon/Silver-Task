import { useMemo, useState } from 'react';
import { AlertTriangle } from 'lucide-react';
import type { Task } from '@/types/task';
import { DEPENDENCY_TYPE_DESCRIPTIONS, DEPENDENCY_TYPE_LABELS, DEPENDENCY_TYPE_OPTIONS, type DependencyType } from '@/types/dependency';
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
  /** Phase 39 — bulk creation: called once per selected task, all with the same relationship
   * type (spec's own "select tasks -> create dependency" bulk flow). Each call goes through the
   * exact same backend validation (cycle/self/cross-project/duplicate) as a single add — there is
   * no separate, less-validated bulk code path. */
  onAdd: (dependsOnTaskId: string, dependencyType: DependencyType) => void;
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
  const [dependencyType, setDependencyType] = useState<DependencyType>('FinishToStart');
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());

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

  function toggleSelected(id: string) {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  }

  function handleSubmit() {
    for (const id of selectedIds) {
      onAdd(id, dependencyType);
    }
  }

  // Finish-to-Start's whole point is "the dependent happens after the prerequisite" — if the
  // prerequisite's own due date is already later than this task's, the schedule as currently set
  // doesn't reflect that ordering yet. A warning, never an automatic date change (per spec) —
  // this app has no automatic scheduling engine to integrate with, so the date is simply left
  // alone and the user decides what to do about it.
  const dateWarnings =
    dependencyType === 'FinishToStart' && task.dueDate
      ? candidates.filter((c) => selectedIds.has(c.id) && c.dueDate && c.dueDate > task.dueDate!)
      : [];

  return (
    <Modal onClose={onClose} size="wide">
      <h2>Add Dependency</h2>
      <p className="add-dependency-dialog__subtitle">
        This task: <strong>{task.title}</strong>
      </p>

      <label className="add-dependency-dialog__type-field">
        <span>Relationship</span>
        <select value={dependencyType} onChange={(e) => setDependencyType(e.target.value as DependencyType)}>
          {DEPENDENCY_TYPE_OPTIONS.map((t) => (
            <option key={t} value={t}>
              {DEPENDENCY_TYPE_LABELS[t]}
            </option>
          ))}
        </select>
      </label>
      <p className="add-dependency-dialog__type-description">{DEPENDENCY_TYPE_DESCRIPTIONS[dependencyType]}</p>

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
          <label key={candidate.id} className="add-dependency-dialog__row">
            <input
              type="checkbox"
              checked={selectedIds.has(candidate.id)}
              disabled={isPending}
              onChange={() => toggleSelected(candidate.id)}
            />
            <span className="add-dependency-dialog__row-title">{candidate.title}</span>
            <div className="add-dependency-dialog__row-meta">
              <StatusBadge status={candidate.status} />
              <PriorityBadge priority={candidate.priority} />
              {candidate.assignedTo && <span className="add-dependency-dialog__row-assignee">{candidate.assignedTo.name}</span>}
            </div>
          </label>
        ))}
      </div>

      {dateWarnings.length > 0 && (
        <div className="add-dependency-dialog__warning">
          <AlertTriangle size={14} />
          <p>
            Warning: this dependency requires &ldquo;{task.title}&rdquo; to occur after {dateWarnings.length === 1 ? 'its prerequisite' : 'these prerequisites'},
            but the current due date{dateWarnings.length === 1 ? ' is' : 's are'} earlier: {dateWarnings.map((c) => c.title).join(', ')}. Due
            dates are left unchanged — you may want to adjust them yourself.
          </p>
        </div>
      )}

      {errorMessage && <p className="form-error">{errorMessage}</p>}

      <div className="add-dependency-dialog__actions">
        <button type="button" className="confirm-delete-dialog__cancel" onClick={onClose}>
          Close
        </button>
        <button type="button" className="add-dependency-dialog__submit" disabled={selectedIds.size === 0 || isPending} onClick={handleSubmit}>
          {isPending ? 'Adding...' : selectedIds.size > 1 ? `Add ${selectedIds.size} Dependencies` : 'Add Dependency'}
        </button>
      </div>
    </Modal>
  );
}
