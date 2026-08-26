import { useState, type KeyboardEvent } from 'react';
import { Folder as FolderIcon, FolderInput, Pencil, RotateCcw, Trash2 } from 'lucide-react';
import type { Folder } from '@/types/folder';
import { useRenameFolder, useRestoreFolder } from '@/hooks/useFolders';
import { DeleteFolderDialog } from './DeleteFolderDialog';
import { MoveFolderDialog } from './MoveFolderDialog';
import './FolderRow.css';

interface FolderRowProps {
  folder: Folder;
  folders: Folder[];
  projectId: string;
  currentUserId: string | undefined;
  /** Files.Upload — the creator may rename/move/delete their own folder at this tier. */
  canUpload: boolean;
  /** Files.Delete — may rename/move/delete/restore any folder. Restore is always this tier
   * regardless of who created the folder, matching Attachment's own Restore rule. */
  canManageFiles: boolean;
  onOpen: (folderId: string) => void;
}

export function FolderRow({ folder, folders, projectId, currentUserId, canUpload, canManageFiles, onOpen }: FolderRowProps) {
  const isOwn = folder.createdBy.id === currentUserId;
  const canModify = canManageFiles || (canUpload && isOwn);
  const renameFolder = useRenameFolder(projectId);
  const restoreFolder = useRestoreFolder(projectId);
  const [isEditing, setIsEditing] = useState(false);
  const [draft, setDraft] = useState(folder.name);
  const [showMove, setShowMove] = useState(false);
  const [showDelete, setShowDelete] = useState(false);

  function startEditing() {
    setDraft(folder.name);
    setIsEditing(true);
  }

  function commitRename() {
    setIsEditing(false);
    const trimmed = draft.trim();
    if (trimmed && trimmed !== folder.name) {
      renameFolder.mutate({ id: folder.id, name: trimmed });
    }
  }

  function handleKeyDown(event: KeyboardEvent<HTMLInputElement>) {
    if (event.key === 'Enter') event.currentTarget.blur();
    if (event.key === 'Escape') setIsEditing(false);
  }

  return (
    <div className={`folder-row${folder.isDeleted ? ' folder-row--deleted' : ''}`}>
      <FolderIcon size={15} className="folder-row__icon" />
      {isEditing ? (
        <input
          className="folder-row__rename-input"
          value={draft}
          onChange={(e) => setDraft(e.target.value)}
          onBlur={commitRename}
          onKeyDown={handleKeyDown}
          autoFocus
        />
      ) : (
        <button type="button" className="folder-row__name" onClick={() => onOpen(folder.id)}>
          {folder.name}
        </button>
      )}

      <div className="folder-row__actions">
        {!folder.isDeleted && canModify && (
          <button type="button" className="icon-button" aria-label={`Rename ${folder.name}`} onClick={startEditing}>
            <Pencil size={13} />
          </button>
        )}
        {!folder.isDeleted && canModify && (
          <button type="button" className="icon-button" aria-label={`Move ${folder.name}`} onClick={() => setShowMove(true)}>
            <FolderInput size={14} />
          </button>
        )}
        {!folder.isDeleted && canModify && (
          <button type="button" className="icon-button" aria-label={`Delete ${folder.name}`} onClick={() => setShowDelete(true)}>
            <Trash2 size={14} />
          </button>
        )}
        {folder.isDeleted && canManageFiles && (
          <button
            type="button"
            className="icon-button"
            aria-label={`Restore ${folder.name}`}
            onClick={() => restoreFolder.mutate(folder.id)}
          >
            <RotateCcw size={14} />
          </button>
        )}
      </div>

      {showMove && <MoveFolderDialog folder={folder} folders={folders} projectId={projectId} onClose={() => setShowMove(false)} />}
      {showDelete && <DeleteFolderDialog folder={folder} projectId={projectId} onClose={() => setShowDelete(false)} />}
    </div>
  );
}
