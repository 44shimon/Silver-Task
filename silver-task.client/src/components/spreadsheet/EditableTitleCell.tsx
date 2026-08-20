import { useState, type KeyboardEvent } from 'react';
import type { Task } from '@/types/task';
import { taskFieldChange, useUpdateTask } from '@/hooks/useTasks';
import './EditableCell.css';

interface EditableTitleCellProps {
  task: Task;
  projectId: string;
}

export function EditableTitleCell({ task, projectId }: EditableTitleCellProps) {
  const updateTask = useUpdateTask(projectId);
  const [isEditing, setIsEditing] = useState(false);
  const [draft, setDraft] = useState(task.title);

  function startEditing() {
    setDraft(task.title);
    setIsEditing(true);
  }

  // onBlur is the single commit path: it fires whether focus left via Tab, a click
  // elsewhere, or Enter (which blurs itself below) — so Tab-to-move-on and Enter-to-commit
  // both funnel through the same optimistic-update logic.
  function commit() {
    setIsEditing(false);
    const trimmed = draft.trim();
    if (trimmed && trimmed !== task.title) {
      updateTask.mutate({ task, change: taskFieldChange.title(trimmed) });
    }
  }

  function handleKeyDown(event: KeyboardEvent<HTMLInputElement>) {
    if (event.key === 'Enter') {
      event.currentTarget.blur();
    } else if (event.key === 'Escape') {
      // Exit without blurring, so commit() never runs — this cancels the edit.
      setIsEditing(false);
    }
  }

  if (isEditing) {
    return (
      <input
        className="editable-cell__input"
        value={draft}
        onChange={(e) => setDraft(e.target.value)}
        onBlur={commit}
        onKeyDown={handleKeyDown}
        autoFocus
      />
    );
  }

  return (
    <div
      className={`editable-cell${updateTask.isError ? ' editable-cell--error' : ''}`}
      tabIndex={0}
      role="button"
      onClick={startEditing}
      onKeyDown={(e) => {
        if (e.key === 'Enter' || e.key === ' ') {
          e.preventDefault();
          startEditing();
        }
      }}
      title={updateTask.isError ? 'Could not save — click to try again' : undefined}
    >
      {task.title}
    </div>
  );
}
