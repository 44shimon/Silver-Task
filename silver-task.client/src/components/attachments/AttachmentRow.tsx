import { useState, type KeyboardEvent } from 'react';
import { Download, Eye, Pencil, RotateCcw, Star, Trash2 } from 'lucide-react';
import type { Attachment } from '@/types/attachment';
import { attachmentsApi } from '@/api/attachmentsApi';
import { useDeleteAttachment, useRenameAttachment, useRestoreAttachment, useToggleFavorite } from '@/hooks/useAttachments';
import { formatFileSize } from '@/utils/formatFileSize';
import { categorizeAttachment } from '@/utils/attachmentType';
import { AttachmentIcon } from './AttachmentIcon';
import './AttachmentRow.css';

const PREVIEWABLE = new Set(['pdf', 'image']);

interface AttachmentRowProps {
  attachment: Attachment;
  currentUserId: string | undefined;
  /** Files.Upload — the uploader may rename/delete their own file at this tier. */
  canUpload: boolean;
  /** Files.Delete — may rename/delete anyone's file, and is the only tier that can restore. */
  canManageFiles: boolean;
  onPreview: (attachment: Attachment) => void;
  /** Shows the compact meta line (location) — used in Project Files where the row's own project
   * context makes it redundant. */
  showLocation?: boolean;
  /** Bulk-select checkbox (Project Files only) — omitted entirely everywhere else. */
  selected?: boolean;
  onToggleSelect?: () => void;
}

export function AttachmentRow({
  attachment,
  currentUserId,
  canUpload,
  canManageFiles,
  onPreview,
  showLocation,
  selected,
  onToggleSelect,
}: AttachmentRowProps) {
  const renameAttachment = useRenameAttachment();
  const deleteAttachment = useDeleteAttachment();
  const restoreAttachment = useRestoreAttachment();
  const toggleFavorite = useToggleFavorite();
  const [isEditing, setIsEditing] = useState(false);
  const [draft, setDraft] = useState(attachment.fileName);

  const isOwn = attachment.uploadedBy.id === currentUserId;
  const canModify = canManageFiles || (canUpload && isOwn);
  const category = categorizeAttachment(attachment.mimeType);
  const canPreview = PREVIEWABLE.has(category) || attachment.mimeType === 'text/plain';

  function startEditing() {
    setDraft(attachment.fileName);
    setIsEditing(true);
  }

  function commitRename() {
    setIsEditing(false);
    const trimmed = draft.trim();
    if (trimmed && trimmed !== attachment.fileName) {
      renameAttachment.mutate({ attachment, fileName: trimmed });
    }
  }

  function handleKeyDown(event: KeyboardEvent<HTMLInputElement>) {
    if (event.key === 'Enter') {
      event.currentTarget.blur();
    } else if (event.key === 'Escape') {
      setIsEditing(false);
    }
  }

  return (
    <div className={`attachment-row${attachment.isDeleted ? ' attachment-row--deleted' : ''}`}>
      {onToggleSelect && (
        <input
          type="checkbox"
          className="attachment-row__select"
          checked={selected ?? false}
          onChange={onToggleSelect}
          aria-label={`Select ${attachment.fileName}`}
        />
      )}
      <AttachmentIcon mimeType={attachment.mimeType} />
      <div className="attachment-row__info">
        {isEditing ? (
          <input
            className="attachment-row__rename-input"
            value={draft}
            onChange={(e) => setDraft(e.target.value)}
            onBlur={commitRename}
            onKeyDown={handleKeyDown}
            autoFocus
          />
        ) : (
          <button type="button" className="attachment-row__name" onClick={() => onPreview(attachment)}>
            {attachment.fileName}
          </button>
        )}
        <span className="attachment-row__meta">
          {formatFileSize(attachment.fileSize)} &middot; {attachment.uploadedBy.name} &middot;{' '}
          {new Date(attachment.createdAt).toLocaleDateString()}
          {attachment.category ? ` · ${attachment.category.name}` : ''}
          {showLocation && attachment.location ? ` · ${attachment.location}` : ''}
        </span>
        {attachment.tags.length > 0 && (
          <span className="attachment-row__tags">
            {attachment.tags.map((tag) => (
              <span className="tag-chip tag-chip--compact" key={tag.id} style={tag.color ? { borderColor: tag.color, color: tag.color } : undefined}>
                {tag.name}
              </span>
            ))}
          </span>
        )}
      </div>

      <div className="attachment-row__actions">
        {!attachment.isDeleted && (
          <button
            type="button"
            className={`icon-button${attachment.isFavorite ? ' attachment-row__favorite--active' : ''}`}
            aria-label={attachment.isFavorite ? `Unfavorite ${attachment.fileName}` : `Favorite ${attachment.fileName}`}
            onClick={() => toggleFavorite.mutate({ attachment, favorite: !attachment.isFavorite })}
          >
            <Star size={13} fill={attachment.isFavorite ? 'currentColor' : 'none'} />
          </button>
        )}
        {canPreview && (
          <button type="button" className="icon-button" aria-label={`Preview ${attachment.fileName}`} onClick={() => onPreview(attachment)}>
            <Eye size={13} />
          </button>
        )}
        <a
          className="icon-button"
          aria-label={`Download ${attachment.fileName}`}
          href={attachmentsApi.downloadUrl(attachment.id)}
          target="_blank"
          rel="noopener noreferrer"
        >
          <Download size={13} />
        </a>
        {!attachment.isDeleted && canModify && (
          <button type="button" className="icon-button" aria-label={`Rename ${attachment.fileName}`} onClick={startEditing}>
            <Pencil size={13} />
          </button>
        )}
        {!attachment.isDeleted && canModify && (
          <button
            type="button"
            className="icon-button"
            aria-label={`Delete ${attachment.fileName}`}
            onClick={() => deleteAttachment.mutate(attachment)}
          >
            <Trash2 size={13} />
          </button>
        )}
        {attachment.isDeleted && canManageFiles && (
          <button
            type="button"
            className="icon-button"
            aria-label={`Restore ${attachment.fileName}`}
            onClick={() => restoreAttachment.mutate(attachment)}
          >
            <RotateCcw size={13} />
          </button>
        )}
      </div>
    </div>
  );
}
