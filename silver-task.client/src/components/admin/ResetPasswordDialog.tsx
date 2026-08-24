import { useState, type FormEvent } from 'react';
import type { AdminUser } from '@/types/admin';
import { useResetUserPassword } from '@/hooks/useAdminUsers';
import { ApiError } from '@/api/httpClient';
import { Modal } from '@/components/shared/Modal';
import './ResetPasswordDialog.css';

interface ResetPasswordDialogProps {
  user: AdminUser;
  onClose: () => void;
}

export function ResetPasswordDialog({ user, onClose }: ResetPasswordDialogProps) {
  const resetPassword = useResetUserPassword();
  const [newPassword, setNewPassword] = useState('');

  function handleSubmit(event: FormEvent) {
    event.preventDefault();
    resetPassword.mutate({ id: user.id, request: { newPassword } }, { onSuccess: onClose });
  }

  return (
    <Modal onClose={onClose}>
      <form onSubmit={handleSubmit}>
        <h2>Reset password</h2>
        <p className="reset-password-dialog__subtitle">Set a new password for {user.name}.</p>

        <label className="reset-password-dialog__field">
          <span>New password</span>
          <input
            type="password"
            value={newPassword}
            onChange={(e) => setNewPassword(e.target.value)}
            minLength={8}
            placeholder="At least 8 characters"
            autoFocus
            required
          />
        </label>

        {resetPassword.isError && (
          <p className="form-error">
            {resetPassword.error instanceof ApiError ? resetPassword.error.message : 'Could not reset password.'}
          </p>
        )}

        <div className="reset-password-dialog__actions">
          <button type="button" className="reset-password-dialog__cancel" onClick={onClose}>
            Cancel
          </button>
          <button type="submit" className="reset-password-dialog__submit" disabled={resetPassword.isPending}>
            {resetPassword.isPending ? 'Resetting...' : 'Reset Password'}
          </button>
        </div>
      </form>
    </Modal>
  );
}
