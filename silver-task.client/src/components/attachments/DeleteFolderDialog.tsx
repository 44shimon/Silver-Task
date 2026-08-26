import { useState } from 'react';
import type { Folder, FolderDeleteMode } from '@/types/folder';
import { useDeleteFolder, useFolderDeletePreview } from '@/hooks/useFolders';
import { Modal } from '@/components/shared/Modal';
import { ApiError } from '@/api/httpClient';
import '@/components/shared/ConfirmDeleteDialog.css';
import './DeleteFolderDialog.css';

interface DeleteFolderDialogProps {
  folder: Folder;
  projectId: string;
  onClose: () => void;
}

/** "This folder contains N files and M subfolders" confirmation (Phase 34) — never destroys
 * contents without an explicit choice between moving them up a level or deleting them along with
 * the folder; an empty folder just deletes normally once the preview confirms there's nothing in it. */
export function DeleteFolderDialog({ folder, projectId, onClose }: DeleteFolderDialogProps) {
  const { data: preview, isLoading } = useFolderDeletePreview(folder.id);
  const deleteFolder = useDeleteFolder(projectId);
  const isEmpty = preview && preview.fileCount === 0 && preview.subfolderCount === 0;
  const [mode, setMode] = useState<FolderDeleteMode>('MoveContentsToParent');

  function handleConfirm() {
    deleteFolder.mutate({ id: folder.id, mode }, { onSuccess: onClose });
  }

  return (
    <Modal onClose={onClose}>
      <h2>Delete &ldquo;{folder.name}&rdquo;?</h2>

      {isLoading && <p>Checking folder contents...</p>}

      {preview && !isEmpty && (
        <>
          <p>
            This folder contains {preview.fileCount} file{preview.fileCount === 1 ? '' : 's'} and {preview.subfolderCount} subfolder
            {preview.subfolderCount === 1 ? '' : 's'}.
          </p>
          <label className="delete-folder-dialog__option">
            <input type="radio" checked={mode === 'MoveContentsToParent'} onChange={() => setMode('MoveContentsToParent')} />
            <span>Move contents to the parent folder</span>
          </label>
          <label className="delete-folder-dialog__option">
            <input type="radio" checked={mode === 'DeleteContents'} onChange={() => setMode('DeleteContents')} />
            <span>Delete this folder and everything inside it</span>
          </label>
        </>
      )}

      {preview && isEmpty && <p>This folder is empty.</p>}

      {deleteFolder.isError && (
        <p className="form-error">
          {deleteFolder.error instanceof ApiError ? deleteFolder.error.message : 'Could not delete folder.'}
        </p>
      )}

      <div className="confirm-delete-dialog__actions">
        <button type="button" className="confirm-delete-dialog__cancel" onClick={onClose} disabled={deleteFolder.isPending}>
          Cancel
        </button>
        <button type="button" className="confirm-delete-dialog__delete" onClick={handleConfirm} disabled={deleteFolder.isPending || isLoading}>
          {deleteFolder.isPending ? 'Deleting...' : 'Delete'}
        </button>
      </div>
    </Modal>
  );
}
