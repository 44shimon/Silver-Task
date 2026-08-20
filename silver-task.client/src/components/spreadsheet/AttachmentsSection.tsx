import type { ChangeEvent } from 'react';
import { Paperclip, Trash2 } from 'lucide-react';
import { useAttachments, useDeleteAttachment, useUploadAttachment } from '@/hooks/useAttachments';
import { attachmentsApi } from '@/api/attachmentsApi';
import { ApiError } from '@/api/httpClient';
import { formatFileSize } from '@/utils/formatFileSize';
import './AttachmentsSection.css';

interface AttachmentsSectionProps {
  taskId: string;
}

export function AttachmentsSection({ taskId }: AttachmentsSectionProps) {
  const { data: attachments } = useAttachments(taskId);
  const uploadAttachment = useUploadAttachment(taskId);
  const deleteAttachment = useDeleteAttachment(taskId);

  function handleFileSelected(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    event.target.value = ''; // allow re-selecting the same file again later
    if (!file) {
      return;
    }
    uploadAttachment.mutate(file);
  }

  return (
    <div className="task-detail-panel__section">
      <h3>Attachments{attachments && attachments.length > 0 ? ` (${attachments.length})` : ''}</h3>

      <div className="attachment-list">
        {attachments?.map((attachment) => (
          <div className="attachment-row" key={attachment.id}>
            <Paperclip size={14} className="attachment-row__icon" />
            <div className="attachment-row__info">
              <a
                className="attachment-row__name"
                href={attachmentsApi.downloadUrl(attachment.id)}
                target="_blank"
                rel="noopener noreferrer"
              >
                {attachment.fileName}
              </a>
              <span className="attachment-row__meta">
                {formatFileSize(attachment.fileSize)} &middot; {attachment.uploadedBy.name} &middot;{' '}
                {new Date(attachment.createdAt).toLocaleDateString()}
              </span>
            </div>
            <button
              type="button"
              className="icon-button"
              aria-label={`Delete ${attachment.fileName}`}
              onClick={() => deleteAttachment.mutate(attachment.id)}
            >
              <Trash2 size={13} />
            </button>
          </div>
        ))}
        {attachments?.length === 0 && <p className="attachment-list__empty">No attachments yet.</p>}
      </div>

      <div className="attachment-upload">
        <input
          type="file"
          id={`attachment-upload-${taskId}`}
          className="attachment-upload__input"
          onChange={handleFileSelected}
          disabled={uploadAttachment.isPending}
        />
        <label htmlFor={`attachment-upload-${taskId}`} className="attachment-upload__label">
          {uploadAttachment.isPending ? 'Uploading...' : '+ Add File'}
        </label>
        {uploadAttachment.isError && (
          <p className="form-error">
            {uploadAttachment.error instanceof ApiError ? uploadAttachment.error.message : 'Could not upload file.'}
          </p>
        )}
      </div>
    </div>
  );
}
