import { useState, type KeyboardEvent } from 'react';
import type { Task } from '@/types/task';
import type { CustomField } from '@/types/customField';
import { useSetTaskCustomValue } from '@/hooks/useTasks';
import './EditableCell.css';

interface TextCustomValueCellProps {
  task: Task;
  field: CustomField;
  projectId: string;
  value: string | null;
}

const INPUT_TYPE_BY_FIELD_TYPE: Partial<Record<string, string>> = {
  Number: 'number',
  Currency: 'number',
  Url: 'url',
  Email: 'email',
  Phone: 'tel',
};

/** Handles Text, LongText, Number, Currency, Url, Email, and Phone — all a click-to-edit
 * free-form value, differing only in input type / multiline / maxLength. */
export function TextCustomValueCell({ task, field, projectId, value }: TextCustomValueCellProps) {
  const setValue = useSetTaskCustomValue(projectId);
  const [isEditing, setIsEditing] = useState(false);
  const [draft, setDraft] = useState('');

  const isMultiline = field.fieldType === 'LongText';

  function startEditing() {
    setDraft(value ?? '');
    setIsEditing(true);
  }

  function commit() {
    setIsEditing(false);
    const trimmed = draft.trim();
    if (trimmed !== (value ?? '')) {
      setValue.mutate({ task, customFieldId: field.id, value: trimmed || null });
    }
  }

  function handleKeyDown(event: KeyboardEvent<HTMLInputElement | HTMLTextAreaElement>) {
    if (event.key === 'Enter' && !isMultiline) {
      event.currentTarget.blur();
    } else if (event.key === 'Escape') {
      setIsEditing(false);
    }
  }

  if (isEditing) {
    return isMultiline ? (
      <textarea
        className="editable-cell__input"
        value={draft}
        onChange={(e) => setDraft(e.target.value)}
        onBlur={commit}
        onKeyDown={handleKeyDown}
        autoFocus
      />
    ) : (
      <input
        type={INPUT_TYPE_BY_FIELD_TYPE[field.fieldType] ?? 'text'}
        step={field.fieldType === 'Currency' ? '0.01' : undefined}
        maxLength={field.maxLength ?? undefined}
        placeholder={field.placeholder ?? undefined}
        className="editable-cell__input"
        value={draft}
        onChange={(e) => setDraft(e.target.value)}
        onBlur={commit}
        onKeyDown={handleKeyDown}
        autoFocus
      />
    );
  }

  const prefix = field.fieldType === 'Currency' && value ? '$' : '';

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
      {value ? `${prefix}${value}` : <span className="editable-cell__placeholder">—</span>}
    </div>
  );
}
