import { useState, type KeyboardEvent } from 'react';
import { Download, Eye, Pencil, RotateCcw, Trash2 } from 'lucide-react';
import type { Attachment } from '@/types/attachment';
import { attachmentsApi } from '@/api/attachmentsApi';
import { useDeleteAttachment, useRenameAttachment, useRestoreAttachment } from '@/hooks/useAttachments';
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
}

export function AttachmentRow({ attachment, currentUserId, canUpload, canManageFiles, onPreview, showLocation }: AttachmentRowProps) {
  const renameAttachment = useRenameAttachment();
  const deleteAttachment = useDeleteAttachment();
  const restoreAttachment = useRestoreAttachment();
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
          {showLocation && attachment.location ? ` · ${attachment.location}` : ''}
        </span>
      </div>

      <div className="attachment-row__actions">
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
