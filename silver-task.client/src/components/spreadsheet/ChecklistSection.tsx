import { useState, type FormEvent } from 'react';
import { Trash2 } from 'lucide-react';
import { useAddChecklistItem, useRemoveChecklistItem, useSetChecklistItemChecked, useTaskChecklist } from '@/hooks/useTaskChecklist';
import { ApiError } from '@/api/httpClient';
import './ChecklistSection.css';

interface ChecklistSectionProps {
  taskId: string;
  canEdit: boolean;
}

/** Phase 40 — a plain checkable list per task. Items are most often seeded by a Task/Project
 * Template's own checklistItems at instantiation time, but can also be added directly here on
 * any task, template-derived or not. Follows the same section-with-add-form shape as
 * SubtasksSection/LabelsSection rather than inventing a new interaction style. */
export function ChecklistSection({ taskId, canEdit }: ChecklistSectionProps) {
  const { data: items } = useTaskChecklist(taskId);
  const addItem = useAddChecklistItem(taskId);
  const setChecked = useSetChecklistItemChecked(taskId);
  const removeItem = useRemoveChecklistItem(taskId);
  const [draft, setDraft] = useState('');
  const [error, setError] = useState<string | null>(null);

  const total = items?.length ?? 0;
  const completed = items?.filter((i) => i.isChecked).length ?? 0;
  const percent = total > 0 ? Math.round((completed / total) * 100) : 0;

  // Nothing to check off and nobody who could add anything — matches the Custom Fields
  // section's own "only show the section if there's something to show" convention.
  if (total === 0 && !canEdit) {
    return null;
  }

  function handleAdd(event: FormEvent) {
    event.preventDefault();
    const trimmed = draft.trim();
    if (!trimmed) return;
    setError(null);
    addItem.mutate(trimmed, {
      onSuccess: () => setDraft(''),
      onError: (err) => setError(err instanceof ApiError ? err.message : 'Could not add checklist item.'),
    });
  }

  return (
    <div className="task-detail-panel__section">
      <h3>Checklist</h3>

      {total > 0 && (
        <div className="checklist-section__progress">
          <span className="checklist-section__progress-label">
            {completed} of {total} complete
          </span>
          <div className="checklist-section__progress-bar">
            <div className="checklist-section__progress-fill" style={{ width: `${percent}%` }} />
          </div>
        </div>
      )}

      {total > 0 && (
        <ul className="checklist-section__list">
          {items!.map((item) => (
            <li key={item.id} className="checklist-section__item">
              <label>
                <input
                  type="checkbox"
                  checked={item.isChecked}
                  disabled={!canEdit || setChecked.isPending}
                  onChange={(e) => setChecked.mutate({ itemId: item.id, isChecked: e.target.checked })}
                />
                <span className={item.isChecked ? 'checklist-section__text--checked' : undefined}>{item.text}</span>
              </label>
              {canEdit && (
                <button type="button" aria-label={`Remove ${item.text}`} onClick={() => removeItem.mutate(item.id)}>
                  <Trash2 size={12} />
                </button>
              )}
            </li>
          ))}
        </ul>
      )}

      {canEdit && (
        <form className="checklist-section__add-form" onSubmit={handleAdd}>
          <input
            type="text"
            placeholder="Add checklist item..."
            value={draft}
            onChange={(e) => setDraft(e.target.value)}
          />
          <button type="submit" disabled={!draft.trim() || addItem.isPending}>
            Add
          </button>
        </form>
      )}
      {error && <p className="form-error">{error}</p>}
    </div>
  );
}
