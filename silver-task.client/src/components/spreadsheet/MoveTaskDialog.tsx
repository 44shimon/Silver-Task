import { useMemo, useState } from 'react';
import type { Task } from '@/types/task';
import { useSetTaskParent } from '@/hooks/useTasks';
import { getTaskDescendantIds } from '@/utils/taskHierarchy';
import { Modal } from '@/components/shared/Modal';
import { ApiError } from '@/api/httpClient';
import '@/components/shared/ConfirmDeleteDialog.css';
import '@/pages/settings/SettingsForm.css';
import './MoveTaskDialog.css';

interface MoveTaskDialogProps {
  task: Task;
  projectId: string;
  tasks: Task[];
  onClose: () => void;
}

const TOP_LEVEL_VALUE = '';

// The explicit "Move Task" action the spec calls for as the first-class way to change hierarchy
// (drag-and-drop reparenting across the whole tree would be a much larger addition — see
// TaskTable's own reorder buttons for the same reasoning). Same-project is already guaranteed
// (every candidate comes from this project's own task list); circular hierarchy is filtered here
// as a UX nicety and re-validated authoritatively by the backend regardless.
export function MoveTaskDialog({ task, projectId, tasks, onClose }: MoveTaskDialogProps) {
  const setParent = useSetTaskParent(projectId);
  const [query, setQuery] = useState('');
  const [selectedParentId, setSelectedParentId] = useState(task.parentTaskId ?? TOP_LEVEL_VALUE);

  const excludedIds = useMemo(() => {
    const ids = getTaskDescendantIds(task, tasks);
    ids.add(task.id);
    return ids;
  }, [task, tasks]);

  const candidates = useMemo(() => {
    const normalized = query.trim().toLowerCase();
    return tasks.filter((candidate) => {
      if (excludedIds.has(candidate.id)) {
        return false;
      }
      if (normalized && !candidate.title.toLowerCase().includes(normalized)) {
        return false;
      }
      return true;
    });
  }, [tasks, excludedIds, query]);

  const hasChanged = selectedParentId !== (task.parentTaskId ?? TOP_LEVEL_VALUE);
  const errorMessage = setParent.isError
    ? setParent.error instanceof ApiError
      ? setParent.error.message
      : 'Could not move task.'
    : null;

  function handleSave() {
    setParent.mutate(
      { taskId: task.id, parentTaskId: selectedParentId || null },
      { onSuccess: onClose },
    );
  }

  return (
    <Modal onClose={onClose} size="wide">
      <h2>Move &ldquo;{task.title}&rdquo;</h2>

      <input
        type="text"
        className="move-task-dialog__search"
        placeholder="Search tasks..."
        value={query}
        onChange={(e) => setQuery(e.target.value)}
        autoFocus
      />

      <div className="move-task-dialog__list">
        <label className="move-task-dialog__row">
          <input
            type="radio"
            name="move-task-parent"
            checked={selectedParentId === TOP_LEVEL_VALUE}
            onChange={() => setSelectedParentId(TOP_LEVEL_VALUE)}
          />
          <span className="move-task-dialog__row-title">Top Level</span>
        </label>
        {candidates.map((candidate) => (
          <label className="move-task-dialog__row" key={candidate.id}>
            <input
              type="radio"
              name="move-task-parent"
              checked={selectedParentId === candidate.id}
              onChange={() => setSelectedParentId(candidate.id)}
            />
            <span className="move-task-dialog__row-title">{candidate.title}</span>
          </label>
        ))}
      </div>

      {errorMessage && <p className="form-error">{errorMessage}</p>}

      <div className="move-task-dialog__actions">
        <button type="button" className="confirm-delete-dialog__cancel" onClick={onClose} disabled={setParent.isPending}>
          Cancel
        </button>
        <button
          type="button"
          className="settings-form__save"
          onClick={handleSave}
          disabled={setParent.isPending || !hasChanged}
        >
          {setParent.isPending ? 'Moving...' : 'Move Task'}
        </button>
      </div>
    </Modal>
  );
}
