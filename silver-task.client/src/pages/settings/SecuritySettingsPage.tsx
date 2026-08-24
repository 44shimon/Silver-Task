import { useState, type FormEvent } from 'react';
import { useChangePassword } from '@/hooks/useUserSettings';
import { ApiError } from '@/api/httpClient';
import './SettingsForm.css';

export function SecuritySettingsPage() {
  const changePassword = useChangePassword();
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmNewPassword, setConfirmNewPassword] = useState('');
  const [mismatch, setMismatch] = useState(false);
  const [justSaved, setJustSaved] = useState(false);

  function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setJustSaved(false);

    // Client-side check is purely for immediate feedback — the backend re-validates the same
    // match (and everything else) independently and is the actual authority.
    if (newPassword !== confirmNewPassword) {
      setMismatch(true);
      return;
    }
    setMismatch(false);

    changePassword.mutate(
      { currentPassword, newPassword, confirmNewPassword },
      {
        onSuccess: () => {
          setCurrentPassword('');
          setNewPassword('');
          setConfirmNewPassword('');
          setJustSaved(true);
        },
      },
    );
  }

  return (
    <form className="settings-form" onSubmit={handleSubmit}>
      <div className="settings-form__field">
        <label htmlFor="security-current-password">Current password</label>
        <input
          id="security-current-password"
          type="password"
          value={currentPassword}
          onChange={(e) => setCurrentPassword(e.target.value)}
          autoComplete="current-password"
          disabled={changePassword.isPending}
          required
        />
      </div>

      <div className="settings-form__field">
        <label htmlFor="security-new-password">New password</label>
        <input
          id="security-new-password"
          type="password"
          value={newPassword}
          onChange={(e) => {
            setNewPassword(e.target.value);
            setMismatch(false);
          }}
          minLength={8}
          placeholder="At least 8 characters"
          autoComplete="new-password"
          disabled={changePassword.isPending}
          required
        />
      </div>

      <div className="settings-form__field">
        <label htmlFor="security-confirm-password">Confirm new password</label>
        <input
          id="security-confirm-password"
          type="password"
          value={confirmNewPassword}
          onChange={(e) => {
            setConfirmNewPassword(e.target.value);
            setMismatch(false);
          }}
          minLength={8}
          autoComplete="new-password"
          disabled={changePassword.isPending}
          required
        />
      </div>

      {mismatch && <p className="form-error">New password and confirmation do not match.</p>}
      {changePassword.isError && (
        <p className="form-error">
          {changePassword.error instanceof ApiError ? changePassword.error.message : 'Could not change password.'}
        </p>
      )}
      {justSaved && <p className="settings-form__success">Password changed.</p>}

      <button type="submit" className="settings-form__save" disabled={changePassword.isPending}>
        {changePassword.isPending ? 'Changing...' : 'Change password'}
      </button>
    </form>
  );
}
