import { useState } from 'react';
import { Link } from 'react-router-dom';
import { Paperclip } from 'lucide-react';
import { useRecentFiles } from '@/hooks/useAttachments';
import { useCurrentUser } from '@/hooks/useAuth';
import { useProjectsByIds } from '@/hooks/useProjects';
import { Permissions } from '@/types/permissions';
import { AttachmentRow } from '@/components/attachments/AttachmentRow';
import { FilePreviewModal } from '@/components/attachments/FilePreviewModal';
import type { Attachment } from '@/types/attachment';
import { DashboardWidget } from './DashboardWidget';
import './NotificationsWidget.css';

// Same per-file, per-project permission resolution as RecentFilesPage (Phase 34/36's own fix for
// "Favorites/Recent" pages) — each row's edit/manage permissions come from *that file's* own
// project, not a single page-level flag.
export function RecentFilesWidget() {
  const { data: currentUser } = useCurrentUser();
  const { data: recent, isLoading, isError, refetch } = useRecentFiles(5);
  const projectsById = useProjectsByIds((recent ?? []).map((a) => a.effectiveProjectId));
  const [previewing, setPreviewing] = useState<Attachment | null>(null);

  function permissionsFor(attachment: Attachment) {
    const myPermissions = projectsById[attachment.effectiveProjectId]?.myPermissions ?? [];
    return {
      canUpload: myPermissions.includes(Permissions.FilesUpload),
      canManageFiles: myPermissions.includes(Permissions.FilesDelete),
    };
  }

  return (
    <DashboardWidget
      title="Recent Files"
      icon={<Paperclip size={14} />}
      isLoading={isLoading}
      isError={isError}
      onRetry={() => refetch()}
      isEmpty={recent?.length === 0}
      emptyTitle="No recent files"
    >
      <div className="attachment-list">
        {recent?.map((attachment) => (
          <AttachmentRow
            key={attachment.id}
            attachment={attachment}
            currentUserId={currentUser?.id}
            {...permissionsFor(attachment)}
            onPreview={setPreviewing}
            showLocation
          />
        ))}
      </div>
      <Link to="/files/recent" className="notifications-widget__view-all">
        View all
      </Link>

      {previewing && (
        <FilePreviewModal
          attachment={previewing}
          projectId={previewing.effectiveProjectId}
          currentUserId={currentUser?.id}
          {...permissionsFor(previewing)}
          onClose={() => setPreviewing(null)}
        />
      )}
    </DashboardWidget>
  );
}
