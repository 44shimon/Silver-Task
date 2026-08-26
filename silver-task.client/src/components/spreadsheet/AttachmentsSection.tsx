import { useState } from 'react';
import { useTaskAttachments, useUploadTaskAttachment } from '@/hooks/useAttachments';
import { useProject } from '@/hooks/useProjects';
import { useCurrentUser } from '@/hooks/useAuth';
import { useProjectPermissions } from '@/hooks/usePermissions';
import { Permissions } from '@/types/permissions';
import { FileDropzone } from '@/components/attachments/FileDropzone';
import { AttachmentRow } from '@/components/attachments/AttachmentRow';
import { FilePreviewModal } from '@/components/attachments/FilePreviewModal';
import { Modal } from '@/components/shared/Modal';
import type { Attachment } from '@/types/attachment';
import './AttachmentsSection.css';

const COMPACT_LIMIT = 3;

interface AttachmentsSectionProps {
  taskId: string;
  projectId: string;
  /** Tasks.Edit tier — kept for backward compatibility with callers that only know the task-edit
   * gate; the real upload/delete gates are Files.Upload/Files.Delete, resolved below from the
   * project's own permission set. */
  canEdit: boolean;
}

export function AttachmentsSection({ taskId, projectId, canEdit }: AttachmentsSectionProps) {
  const { data: attachments } = useTaskAttachments(taskId);
  const { data: project } = useProject(projectId);
  const { data: currentUser } = useCurrentUser();
  const { can } = useProjectPermissions(project);
  const canUploadFiles = canEdit && can(Permissions.FilesUpload);
  const canManageFiles = can(Permissions.FilesDelete);
  const uploadAttachment = useUploadTaskAttachment(taskId);
  const [previewing, setPreviewing] = useState<Attachment | null>(null);
  const [showAll, setShowAll] = useState(false);

  const visible = attachments ?? [];
  const compactList = visible.slice(0, COMPACT_LIMIT);

  return (
    <div className="task-detail-panel__section">
      <h3>Attachments{visible.length > 0 ? ` (${visible.length})` : ''}</h3>

      {canUploadFiles && (
        <FileDropzone
          compact
          onUpload={(file, onProgress) => uploadAttachment.mutateAsync({ file, onProgress })}
        />
      )}

      <div className="attachment-list">
        {compactList.map((attachment) => (
          <AttachmentRow
            key={attachment.id}
            attachment={attachment}
            currentUserId={currentUser?.id}
            canUpload={canUploadFiles}
            canManageFiles={canManageFiles}
            onPreview={setPreviewing}
          />
        ))}
        {visible.length === 0 && <p className="attachment-list__empty">No attachments yet.</p>}
      </div>

      {visible.length > COMPACT_LIMIT && (
        <button type="button" className="attachment-list__view-all" onClick={() => setShowAll(true)}>
          View all ({visible.length})
        </button>
      )}

      {previewing && <FilePreviewModal attachment={previewing} onClose={() => setPreviewing(null)} />}

      {showAll && (
        <Modal onClose={() => setShowAll(false)} size="wide">
          <h2>Attachments ({visible.length})</h2>
          <div className="attachment-list">
            {visible.map((attachment) => (
              <AttachmentRow
                key={attachment.id}
                attachment={attachment}
                currentUserId={currentUser?.id}
                canUpload={canUploadFiles}
                canManageFiles={canManageFiles}
                onPreview={setPreviewing}
              />
            ))}
          </div>
        </Modal>
      )}
    </div>
  );
}
