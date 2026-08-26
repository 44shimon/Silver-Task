import { Download } from 'lucide-react';
import type { Attachment } from '@/types/attachment';
import { attachmentsApi } from '@/api/attachmentsApi';
import { formatFileSize } from '@/utils/formatFileSize';
import { categorizeAttachment } from '@/utils/attachmentType';
import { Modal } from '@/components/shared/Modal';
import './FilePreviewModal.css';

interface FilePreviewModalProps {
  attachment: Attachment;
  onClose: () => void;
}

/** Clicking a file anywhere in the app (task attachments, project files, comment attachments)
 * opens this — combines the spec's "click a file shows info" requirement with in-browser preview
 * for the types that support it (image thumbnail-to-enlarge, PDF, plain text). Every other type
 * just shows the info panel with a Download action — no OnlyOffice/office-doc editor exists in
 * this app to preview .doc/.docx/.xls/.xlsx with (confirmed absent — out of scope to build one). */
export function FilePreviewModal({ attachment, onClose }: FilePreviewModalProps) {
  const category = categorizeAttachment(attachment.mimeType);
  const downloadUrl = attachmentsApi.downloadUrl(attachment.id);
  const canEmbedPreview = category === 'pdf' || category === 'image' || attachment.mimeType === 'text/plain';

  return (
    <Modal onClose={onClose} size="xl">
      <div className="file-preview-modal__header">
        <h2>{attachment.fileName}</h2>
        <a className="icon-button" href={downloadUrl} target="_blank" rel="noopener noreferrer" aria-label="Download">
          <Download size={16} />
        </a>
      </div>

      {canEmbedPreview && (
        <div className="file-preview-modal__preview">
          {category === 'image' ? (
            <img src={downloadUrl} alt={attachment.fileName} className="file-preview-modal__image" />
          ) : (
            <iframe src={downloadUrl} title={attachment.fileName} className="file-preview-modal__frame" />
          )}
        </div>
      )}

      <dl className="file-preview-modal__info">
        <dt>File name</dt>
        <dd>{attachment.fileName}</dd>
        <dt>Type</dt>
        <dd>{attachment.mimeType}</dd>
        <dt>Size</dt>
        <dd>{formatFileSize(attachment.fileSize)}</dd>
        <dt>Uploaded by</dt>
        <dd>{attachment.uploadedBy.name}</dd>
        <dt>Uploaded</dt>
        <dd>{new Date(attachment.createdAt).toLocaleString()}</dd>
        <dt>Last modified</dt>
        <dd>{new Date(attachment.updatedAt).toLocaleString()}</dd>
        <dt>Location</dt>
        <dd>{attachment.location}</dd>
      </dl>
    </Modal>
  );
}
