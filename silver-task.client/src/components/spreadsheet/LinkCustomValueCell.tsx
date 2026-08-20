import { useState, type KeyboardEvent } from 'react';
import { ExternalLink, Pencil, Plus } from 'lucide-react';
import type { Task } from '@/types/task';
import type { CustomField, LinkValue } from '@/types/customField';
import { useSetTaskCustomValue } from '@/hooks/useTasks';
import './LinkCustomValueCell.css';

interface LinkCustomValueCellProps {
  task: Task;
  field: CustomField;
  projectId: string;
  value: string | null;
}

export function LinkCustomValueCell({ task, field, projectId, value }: LinkCustomValueCellProps) {
  const setValue = useSetTaskCustomValue(projectId);
  const [isEditing, setIsEditing] = useState(false);
  const [labelDraft, setLabelDraft] = useState('');
  const [urlDraft, setUrlDraft] = useState('');

  const parsed = parseLinkValue(value);

  function startEditing() {
    setLabelDraft(parsed?.label ?? '');
    setUrlDraft(parsed?.url ?? '');
    setIsEditing(true);
  }

  function cancel() {
    setIsEditing(false);
  }

  function save() {
    const trimmedUrl = urlDraft.trim();
    const trimmedLabel = labelDraft.trim();
    setIsEditing(false);

    if (!trimmedUrl) {
      if (parsed) {
        setValue.mutate({ task, customFieldId: field.id, value: null });
      }
      return;
    }

    const newValue = JSON.stringify({ label: trimmedLabel, url: trimmedUrl });
    if (newValue !== value) {
      setValue.mutate({ task, customFieldId: field.id, value: newValue });
    }
  }

  function handleKeyDown(event: KeyboardEvent<HTMLInputElement>) {
    if (event.key === 'Enter') {
      save();
    } else if (event.key === 'Escape') {
      cancel();
    }
  }

  if (isEditing) {
    return (
      <div className="link-cell">
        <div className="link-cell-editor">
          <input
            type="text"
            placeholder="Site name (optional)"
            value={labelDraft}
            onChange={(e) => setLabelDraft(e.target.value)}
            onKeyDown={handleKeyDown}
            autoFocus
          />
          <input
            type="url"
            placeholder="https://..."
            value={urlDraft}
            onChange={(e) => setUrlDraft(e.target.value)}
            onKeyDown={handleKeyDown}
          />
          <div className="link-cell-editor__actions">
            <button type="button" className="link-cell-editor__save" onClick={save}>
              Save
            </button>
            <button type="button" className="link-cell-editor__cancel" onClick={cancel}>
              Cancel
            </button>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className={`link-cell${setValue.isError ? ' link-cell--error' : ''}`} title={setValue.isError ? 'Could not save — try again' : undefined}>
      {parsed ? (
        <>
          <a
            href={parsed.url}
            target="_blank"
            rel="noopener noreferrer"
            className="link-cell__button"
            onClick={(e) => e.stopPropagation()}
          >
            <ExternalLink size={12} />
            <span>{parsed.label || shortenUrl(parsed.url)}</span>
          </a>
          <button type="button" className="link-cell__edit-trigger" onClick={startEditing} aria-label={`Edit ${field.name}`}>
            <Pencil size={11} />
          </button>
        </>
      ) : (
        <button type="button" className="link-cell__add-button" onClick={startEditing}>
          <Plus size={11} />
          <span>Add Link</span>
        </button>
      )}
    </div>
  );
}

function parseLinkValue(value: string | null): LinkValue | null {
  if (!value) {
    return null;
  }
  try {
    const parsed = JSON.parse(value) as Partial<LinkValue>;
    if (typeof parsed.url === 'string' && parsed.url) {
      return { label: typeof parsed.label === 'string' ? parsed.label : '', url: parsed.url };
    }
    return null;
  } catch {
    return null;
  }
}

function shortenUrl(url: string): string {
  try {
    return new URL(url).hostname.replace(/^www\./, '');
  } catch {
    return url;
  }
}
