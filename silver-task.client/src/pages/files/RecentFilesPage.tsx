import { useState } from 'react';
import { useRecentFiles } from '@/hooks/useAttachments';
import { useCurrentUser } from '@/hooks/useAuth';
import { AttachmentRow } from '@/components/attachments/AttachmentRow';
import { FilePreviewModal } from '@/components/attachments/FilePreviewModal';
import type { Attachment } from '@/types/attachment';
import './FilesListPage.css';

/** Files -> Recent — files the caller has uploaded or modified most recently, limited to
 * projects they can still access (see IAttachmentService.GetRecentAsync's own doc comment for
 * why this reuses Attachment's existing timestamps rather than a new access-log table). */
export function RecentFilesPage() {
  const { data: currentUser } = useCurrentUser();
  const { data: recent, isLoading, isError } = useRecentFiles();
  const [previewing, setPreviewing] = useState<Attachment | null>(null);

  return (
    <div className="files-list-page">
      <h1>Recent Files</h1>

      {isLoading && <p>Loading...</p>}
      {isError && <p className="form-error">Recent files could not be loaded.</p>}
      {!isLoading && !isError && recent?.length === 0 && <p className="attachment-list__empty">No recent files.</p>}

      {!isLoading && !isError && recent && recent.length > 0 && (
        <div className="attachment-list">
          {recent.map((attachment) => (
            <AttachmentRow
              key={attachment.id}
              attachment={attachment}
              currentUserId={currentUser?.id}
              canUpload={false}
              canManageFiles={false}
              onPreview={setPreviewing}
              showLocation
            />
          ))}
        </div>
      )}

      {previewing && (
        <FilePreviewModal
          attachment={previewing}
          projectId={previewing.effectiveProjectId}
          currentUserId={currentUser?.id}
          canUpload={false}
          canManageFiles={false}
          onClose={() => setPreviewing(null)}
        />
      )}
    </div>
  );
}
