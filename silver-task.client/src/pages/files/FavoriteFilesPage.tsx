import { useState } from 'react';
import { useFavoriteFiles } from '@/hooks/useAttachments';
import { useCurrentUser } from '@/hooks/useAuth';
import { AttachmentRow } from '@/components/attachments/AttachmentRow';
import { FilePreviewModal } from '@/components/attachments/FilePreviewModal';
import type { Attachment } from '@/types/attachment';
import './FilesListPage.css';

/** Files -> Favorites — every file the caller has favorited that they can still currently
 * access (re-checked live server-side; see IAttachmentService.GetFavoritesAsync). Spans every
 * project, so rename/move/category actions are deliberately not exposed here — only
 * preview/download/unfavorite; full management stays on the file's own Project Files tab. */
export function FavoriteFilesPage() {
  const { data: currentUser } = useCurrentUser();
  const { data: favorites, isLoading, isError } = useFavoriteFiles();
  const [previewing, setPreviewing] = useState<Attachment | null>(null);

  return (
    <div className="files-list-page">
      <h1>Favorites</h1>

      {isLoading && <p>Loading...</p>}
      {isError && <p className="form-error">Favorites could not be loaded.</p>}
      {!isLoading && !isError && favorites?.length === 0 && <p className="attachment-list__empty">No favorites yet.</p>}

      {!isLoading && !isError && favorites && favorites.length > 0 && (
        <div className="attachment-list">
          {favorites.map((attachment) => (
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
