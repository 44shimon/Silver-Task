import { useRef, useState, type FormEvent } from 'react';
import { KeyRound } from 'lucide-react';
import { useResetUserPassword } from '@/hooks/useAdminUsers';
import { ApiError } from '@/api/httpClient';
import '@/components/spreadsheet/Toolbar.css';
import './ResetPasswordButton.css';

interface ResetPasswordButtonProps {
  userId: string;
  userName: string;
}

export function ResetPasswordButton({ userId, userName }: ResetPasswordButtonProps) {
  const resetPassword = useResetUserPassword();
  const detailsRef = useRef<HTMLDetailsElement>(null);
  const [newPassword, setNewPassword] = useState('');

  function handleSubmit(event: FormEvent) {
    event.preventDefault();
    resetPassword.mutate(
      { id: userId, request: { newPassword } },
      {
        onSuccess: () => {
          setNewPassword('');
          resetPassword.reset();
          if (detailsRef.current) {
            detailsRef.current.open = false;
          }
        },
      },
    );
  }

  return (
    <details className="toolbar-popover reset-password-popover" ref={detailsRef}>
      <summary className="reset-password-popover__trigger" aria-label={`Reset password for ${userName}`} title="Reset password">
        <KeyRound size={14} />
      </summary>
      <div className="toolbar-popover__panel">
        <form onSubmit={handleSubmit}>
          <label className="toolbar-popover__field">
            <span>New password for {userName}</span>
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
          <button type="submit" className="reset-password-popover__submit" disabled={resetPassword.isPending}>
            Reset Password
          </button>
          {resetPassword.isError && (
            <p className="form-error">
              {resetPassword.error instanceof ApiError ? resetPassword.error.message : 'Could not reset password.'}
            </p>
          )}
        </form>
      </div>
    </details>
  );
}
