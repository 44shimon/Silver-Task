import { useState, type FormEvent } from 'react';
import { useCurrentUser } from '@/hooks/useAuth';
import { useUpdateProfile } from '@/hooks/useUserSettings';
import { ApiError } from '@/api/httpClient';
import { formatDateTime } from '@/utils/formatDate';
import './SettingsForm.css';

// Name/Email only, by design — there is no Role field on this form at all (not just disabled),
// since the backend's UpdateProfileRequest structurally has nowhere to carry a role change.
// Role/Created are shown read-only below the form instead.
export function ProfileSettingsPage() {
  const { data: user } = useCurrentUser();
  const updateProfile = useUpdateProfile();
  const [name, setName] = useState('');
  const [email, setEmail] = useState('');
  const [justSaved, setJustSaved] = useState(false);
  // React's own recommended "adjust state during render" pattern for seeding local editable
  // state from a query result, instead of a useEffect(() => setState(...)) that would just
  // trigger an extra render after the one that already has the data.
  const [loadedUserId, setLoadedUserId] = useState<string | undefined>(undefined);
  if (user && user.id !== loadedUserId) {
    setLoadedUserId(user.id);
    setName(user.name);
    setEmail(user.email);
  }

  function handleSubmit(event: FormEvent) {
    event.preventDefault();
    const trimmedName = name.trim();
    const trimmedEmail = email.trim();
    if (!trimmedName || !trimmedEmail) {
      return;
    }

    setJustSaved(false);
    updateProfile.mutate(
      { name: trimmedName, email: trimmedEmail },
      { onSuccess: () => setJustSaved(true) },
    );
  }

  if (!user) {
    return <p>Loading...</p>;
  }

  return (
    <form className="settings-form" onSubmit={handleSubmit}>
      <div className="settings-form__field">
        <label htmlFor="profile-name">Display name</label>
        <input
          id="profile-name"
          type="text"
          value={name}
          onChange={(e) => {
            setName(e.target.value);
            setJustSaved(false);
          }}
          disabled={updateProfile.isPending}
        />
      </div>

      <div className="settings-form__field">
        <label htmlFor="profile-email">Email</label>
        <input
          id="profile-email"
          type="email"
          value={email}
          onChange={(e) => {
            setEmail(e.target.value);
            setJustSaved(false);
          }}
          disabled={updateProfile.isPending}
        />
      </div>

      <div className="settings-form__readonly">
        <span className="settings-form__readonly-label">Role</span>
        <span className="settings-form__readonly-value">{user.role}</span>
      </div>

      <div className="settings-form__readonly">
        <span className="settings-form__readonly-label">Account created</span>
        <span className="settings-form__readonly-value">{formatDateTime(user.createdAt)}</span>
      </div>

      {updateProfile.isError && (
        <p className="form-error">
          {updateProfile.error instanceof ApiError ? updateProfile.error.message : 'Could not save profile.'}
        </p>
      )}
      {justSaved && !updateProfile.isError && <p className="settings-form__success">Profile saved.</p>}

      <button type="submit" className="settings-form__save" disabled={updateProfile.isPending}>
        {updateProfile.isPending ? 'Saving...' : 'Save changes'}
      </button>
    </form>
  );
}
