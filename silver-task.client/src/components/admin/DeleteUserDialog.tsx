import { useState } from 'react';
import type { AdminUser } from '@/types/admin';
import { useDeleteUser, useUserDeletionImpact } from '@/hooks/useAdminUsers';
import { ApiError } from '@/api/httpClient';
import { Modal } from '@/components/shared/Modal';
import './DeleteUserDialog.css';

interface DeleteUserDialogProps {
  user: AdminUser;
  onClose: () => void;
}

// Deletion is a soft delete server-side (see UserService.DeleteAsync) — nothing shown here is
// actually destroyed, but a high-risk-looking action still gets the same "type to confirm"
// friction as an irreversible one, since from the admin's perspective the user disappears from
// every active list and can no longer log in either way.
export function DeleteUserDialog({ user, onClose }: DeleteUserDialogProps) {
  const { data: impact, isLoading } = useUserDeletionImpact(user.id);
  const deleteUser = useDeleteUser();
  const [confirmText, setConfirmText] = useState('');

  const isConfirmed = confirmText.trim().toLowerCase() === user.email.toLowerCase();

  function handleDelete() {
    if (!isConfirmed) {
      return;
    }
    deleteUser.mutate(user.id, { onSuccess: onClose });
  }

  return (
    <Modal onClose={onClose}>
      <h2>Delete {user.name}?</h2>

      {isLoading && <p className="delete-user-dialog__loading">Loading account details...</p>}

      {impact && (
        <dl className="delete-user-dialog__facts">
          <div>
            <dt>Email</dt>
            <dd>{impact.email}</dd>
          </div>
          <div>
            <dt>Role</dt>
            <dd>{impact.role}</dd>
          </div>
          <div>
            <dt>Tasks assigned</dt>
            <dd>{impact.assignedTaskCount}</dd>
          </div>
          <div>
            <dt>Projects</dt>
            <dd>{impact.projectCount}</dd>
          </div>
          <div>
            <dt>Comments</dt>
            <dd>{impact.commentCount}</dd>
          </div>
          <div>
            <dt>Activity records</dt>
            <dd>{impact.activityCount}</dd>
          </div>
        </dl>
      )}

      <p className="delete-user-dialog__warning">
        This account will stop being able to log in and disappear from active user lists. Their existing tasks,
        comments, and activity history are preserved, not destroyed.
      </p>

      <label className="delete-user-dialog__confirm-field">
        <span>
          Type <strong>{user.email}</strong> to confirm deletion
        </span>
        <input
          type="text"
          value={confirmText}
          onChange={(e) => setConfirmText(e.target.value)}
          placeholder={user.email}
          autoFocus
        />
      </label>

      {deleteUser.isError && (
        <p className="form-error">
          {deleteUser.error instanceof ApiError ? deleteUser.error.message : 'Could not delete user.'}
        </p>
      )}

      <div className="delete-user-dialog__actions">
        <button type="button" className="delete-user-dialog__cancel" onClick={onClose} disabled={deleteUser.isPending}>
          Cancel
        </button>
        <button
          type="button"
          className="delete-user-dialog__delete"
          onClick={handleDelete}
          disabled={!isConfirmed || deleteUser.isPending}
        >
          {deleteUser.isPending ? 'Deleting...' : 'Delete User'}
        </button>
      </div>
    </Modal>
  );
}
