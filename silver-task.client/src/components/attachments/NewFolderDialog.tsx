import { useState, type FormEvent } from 'react';
import { useCreateFolder } from '@/hooks/useFolders';
import { Modal } from '@/components/shared/Modal';
import { ApiError } from '@/api/httpClient';
import '@/components/shared/ConfirmDeleteDialog.css';
import '@/pages/settings/SettingsForm.css';

interface NewFolderDialogProps {
  projectId: string;
  parentFolderId: string | null;
  onClose: () => void;
}

export function NewFolderDialog({ projectId, parentFolderId, onClose }: NewFolderDialogProps) {
  const createFolder = useCreateFolder(projectId);
  const [name, setName] = useState('');

  function handleSubmit(event: FormEvent) {
    event.preventDefault();
    const trimmed = name.trim();
    if (!trimmed) {
      return;
    }
    createFolder.mutate({ name: trimmed, parentFolderId }, { onSuccess: onClose });
  }

  return (
    <Modal onClose={onClose}>
      <form onSubmit={handleSubmit}>
        <h2>New Folder</h2>
        <div className="settings-form__field">
          <label>Folder name</label>
          <input type="text" value={name} onChange={(e) => setName(e.target.value)} autoFocus />
        </div>

        {createFolder.isError && (
          <p className="form-error">
            {createFolder.error instanceof ApiError ? createFolder.error.message : 'Could not create folder.'}
          </p>
        )}

        <div className="confirm-delete-dialog__actions">
          <button type="button" className="confirm-delete-dialog__cancel" onClick={onClose} disabled={createFolder.isPending}>
            Cancel
          </button>
          <button type="submit" className="settings-form__save" disabled={createFolder.isPending || !name.trim()}>
            {createFolder.isPending ? 'Creating...' : 'Create'}
          </button>
        </div>
      </form>
    </Modal>
  );
}
