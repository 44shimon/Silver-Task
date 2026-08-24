import { useState, type FormEvent } from 'react';
import type { AdminUser } from '@/types/admin';
import { useResetUserPassword } from '@/hooks/useAdminUsers';
import { ApiError } from '@/api/httpClient';
import './ResetPasswordDialog.css';

interface ResetPasswordDialogProps {
  user: AdminUser;
  onClose: () => void;
}

// Rendered at the page level (a sibling of the table, not nested inside a clipped table cell)
// as a centered modal — same fixed-backdrop technique as TaskDetailPanel's side drawer, just
// centered instead of docked, since this is a single focused action rather than a whole record.
export function ResetPasswordDialog({ user, onClose }: ResetPasswordDialogProps) {
  const resetPassword = useResetUserPassword();
  const [newPassword, setNewPassword] = useState('');

  function handleSubmit(event: FormEvent) {
    event.preventDefault();
    resetPassword.mutate({ id: user.id, request: { newPassword } }, { onSuccess: onClose });
  }

  return (
    <div className="reset-password-backdrop" onClick={onClose}>
      <form className="reset-password-dialog" onClick={(e) => e.stopPropagation()} onSubmit={handleSubmit}>
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
    </div>
  );
}
