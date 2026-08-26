import { useState, type KeyboardEvent } from 'react';
import type { Task } from '@/types/task';
import { taskFieldChange, useUpdateTask } from '@/hooks/useTasks';
import { formatDate } from '@/utils/formatDate';
import './EditableCell.css';

interface EditableDateCellProps {
  task: Task;
  projectId: string;
  field: 'startDate' | 'dueDate';
  readOnly?: boolean;
}

export function EditableDateCell({ task, projectId, field, readOnly }: EditableDateCellProps) {
  const updateTask = useUpdateTask(projectId);
  const [isEditing, setIsEditing] = useState(false);
  const [draft, setDraft] = useState('');

  const value = task[field];

  function startEditing() {
    setDraft(value ?? '');
    setIsEditing(true);
  }

  function commit() {
    setIsEditing(false);
    const newValue = draft || null;
    if (newValue !== value) {
      const change = field === 'startDate' ? taskFieldChange.startDate(newValue) : taskFieldChange.dueDate(newValue);
      updateTask.mutate({ task, change });
    }
  }

  function handleKeyDown(event: KeyboardEvent<HTMLInputElement>) {
    if (event.key === 'Enter') {
      event.currentTarget.blur();
    } else if (event.key === 'Escape') {
      setIsEditing(false);
    }
  }

  if (isEditing) {
    return (
      <input
        type="date"
        className="editable-cell__input"
        value={draft}
        onChange={(e) => setDraft(e.target.value)}
        onBlur={commit}
        onKeyDown={handleKeyDown}
        autoFocus
      />
    );
  }

  const display = formatDate(value);

  if (readOnly) {
    return <div className="editable-cell editable-cell--readonly">{display || <span className="editable-cell__placeholder">—</span>}</div>;
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
      {display || <span className="editable-cell__placeholder">—</span>}
    </div>
  );
}
