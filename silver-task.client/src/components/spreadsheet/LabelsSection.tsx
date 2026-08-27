import { useState } from 'react';
import { X } from 'lucide-react';
import { useAddTaskLabel, useRemoveTaskLabel, useTaskLabels } from '@/hooks/useTaskLabels';
import { ApiError } from '@/api/httpClient';
import './LabelsSection.css';

interface LabelsSectionProps {
  taskId: string;
  canEdit: boolean;
}

// Mirrors FilePreviewModal's Tags field (Phase 34) exactly — same click-to-add-chip interaction,
// same shared global Tag vocabulary (reused via TaskTag, see the backend's own doc comment on
// why this isn't a second label system).
export function LabelsSection({ taskId, canEdit }: LabelsSectionProps) {
  const { data: labels } = useTaskLabels(taskId);
  const addLabel = useAddTaskLabel(taskId);
  const removeLabel = useRemoveTaskLabel(taskId);
  const [draft, setDraft] = useState('');
  const [error, setError] = useState<string | null>(null);

  function handleAdd() {
    const trimmed = draft.trim();
    if (!trimmed) return;
    setError(null);
    addLabel.mutate(trimmed, {
      onSuccess: () => setDraft(''),
      onError: (err) => setError(err instanceof ApiError ? err.message : 'Could not add label.'),
    });
  }

  return (
    <div className="task-detail-panel__field">
      <span className="task-detail-panel__label">Labels</span>
      <div className="labels-section__chips">
        {labels?.map((label) => (
          <span className="tag-chip" key={label.id} style={label.color ? { borderColor: label.color, color: label.color } : undefined}>
            {label.name}
            {canEdit && (
              <button type="button" aria-label={`Remove label ${label.name}`} onClick={() => removeLabel.mutate(label.id)}>
                <X size={10} />
              </button>
            )}
          </span>
        ))}
        {canEdit && (
          <span className="labels-section__add">
            <input
              type="text"
              placeholder="Add label..."
              value={draft}
              onChange={(e) => setDraft(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === 'Enter') {
                  e.preventDefault();
                  handleAdd();
                }
              }}
            />
            <button type="button" onClick={handleAdd} disabled={!draft.trim() || addLabel.isPending}>
              Add
            </button>
          </span>
        )}
      </div>
      {error && <p className="form-error">{error}</p>}
    </div>
  );
}
