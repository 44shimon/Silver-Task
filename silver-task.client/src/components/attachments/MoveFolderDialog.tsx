import { useState } from 'react';
import type { Folder } from '@/types/folder';
import { useMoveFolder } from '@/hooks/useFolders';
import { buildFolderOptions, getDescendantFolderIds } from '@/utils/folderTree';
import { Modal } from '@/components/shared/Modal';
import { ApiError } from '@/api/httpClient';
import '@/components/shared/ConfirmDeleteDialog.css';
import '@/pages/settings/SettingsForm.css';

interface MoveFolderDialogProps {
  folder: Folder;
  folders: Folder[];
  projectId: string;
  onClose: () => void;
}

export function MoveFolderDialog({ folder, folders, projectId, onClose }: MoveFolderDialogProps) {
  const moveFolder = useMoveFolder(projectId);
  const [targetId, setTargetId] = useState(folder.parentFolderId ?? '');

  // A folder can never move into itself or any of its own descendants — filtered client-side
  // for a sane picker; the backend re-validates this independently regardless (IsDescendantOfAsync).
  const excluded = new Set([folder.id, ...getDescendantFolderIds(folder.id, folders)]);
  const options = buildFolderOptions(folders).filter((option) => option.id === null || !excluded.has(option.id));

  function handleMove() {
    moveFolder.mutate({ id: folder.id, parentFolderId: targetId || null }, { onSuccess: onClose });
  }

  return (
    <Modal onClose={onClose}>
      <h2>Move &ldquo;{folder.name}&rdquo;</h2>

      <div className="settings-form__field">
        <label>Destination</label>
        <select value={targetId} onChange={(e) => setTargetId(e.target.value)}>
          {options.map((option) => (
            <option key={option.id ?? 'root'} value={option.id ?? ''}>
              {'  '.repeat(option.depth)}
              {option.label}
            </option>
          ))}
        </select>
      </div>

      {moveFolder.isError && (
        <p className="form-error">{moveFolder.error instanceof ApiError ? moveFolder.error.message : 'Could not move folder.'}</p>
      )}

      <div className="confirm-delete-dialog__actions">
        <button type="button" className="confirm-delete-dialog__cancel" onClick={onClose} disabled={moveFolder.isPending}>
          Cancel
        </button>
        <button
          type="button"
          className="settings-form__save"
          onClick={handleMove}
          disabled={moveFolder.isPending || targetId === (folder.parentFolderId ?? '')}
        >
          {moveFolder.isPending ? 'Moving...' : 'Move'}
        </button>
      </div>
    </Modal>
  );
}
