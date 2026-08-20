import { useState, type KeyboardEvent } from 'react';
import type { Task } from '@/types/task';
import type { CustomField } from '@/types/customField';
import { useSetTaskCustomValue } from '@/hooks/useTasks';
import { formatDate } from '@/utils/formatDate';
import './EditableCell.css';

interface DateCustomValueCellProps {
  task: Task;
  field: CustomField;
  projectId: string;
  value: string | null;
}

export function DateCustomValueCell({ task, field, projectId, value }: DateCustomValueCellProps) {
  const setValue = useSetTaskCustomValue(projectId);
  const [isEditing, setIsEditing] = useState(false);
  const [draft, setDraft] = useState('');

  const isDateTime = field.fieldType === 'DateTime';

  function toInputValue(raw: string | null): string {
    if (!raw) {
      return '';
    }
    if (!isDateTime) {
      return raw; // already "YYYY-MM-DD"
    }
    const parsed = new Date(raw);
    if (Number.isNaN(parsed.getTime())) {
      return '';
    }
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${parsed.getFullYear()}-${pad(parsed.getMonth() + 1)}-${pad(parsed.getDate())}T${pad(parsed.getHours())}:${pad(parsed.getMinutes())}`;
  }

  function startEditing() {
    setDraft(toInputValue(value));
    setIsEditing(true);
  }

  function commit() {
    setIsEditing(false);
    const newValue = draft ? (isDateTime ? new Date(draft).toISOString() : draft) : null;
    if (newValue !== value) {
      setValue.mutate({ task, customFieldId: field.id, value: newValue });
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
        type={isDateTime ? 'datetime-local' : 'date'}
        className="editable-cell__input"
        value={draft}
        onChange={(e) => setDraft(e.target.value)}
        onBlur={commit}
        onKeyDown={handleKeyDown}
        autoFocus
      />
    );
  }

  const display = isDateTime && value ? new Date(value).toLocaleString() : formatDate(value);

  return (
    <div
      className={`editable-cell${setValue.isError ? ' editable-cell--error' : ''}`}
      tabIndex={0}
      role="button"
      onClick={startEditing}
      onKeyDown={(e) => {
        if (e.key === 'Enter' || e.key === ' ') {
          e.preventDefault();
          startEditing();
        }
      }}
      title={setValue.isError ? 'Could not save — click to try again' : undefined}
    >
      {display || <span className="editable-cell__placeholder">—</span>}
    </div>
  );
}
