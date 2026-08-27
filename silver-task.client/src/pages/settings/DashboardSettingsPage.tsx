import { Link } from 'react-router-dom';
import { useUpdatePreferences, useUserPreferences } from '@/hooks/useUserSettings';
import type { DefaultLandingPage } from '@/types/settings';
import { ApiError } from '@/api/httpClient';
import './SettingsForm.css';

const LANDING_OPTIONS: { id: DefaultLandingPage; label: string; description: string }[] = [
  { id: 'Dashboard', label: 'Dashboard', description: 'Your personal workspace — task summary, overdue, upcoming, and more.' },
  { id: 'MyTasks', label: 'My Tasks', description: 'Straight to the full My Tasks list.' },
  { id: 'LastVisited', label: 'Last visited page', description: 'Wherever you were before you last left this browser.' },
];

// Widget visibility/order are edited directly on the Dashboard itself (Customize Dashboard) —
// not duplicated here as a second editor for the same DashboardLayout preference.
export function DashboardSettingsPage() {
  const { data: preferences, isLoading, isError } = useUserPreferences();
  const updatePreferences = useUpdatePreferences();

  function setLandingPage(defaultLandingPage: DefaultLandingPage) {
    if (!preferences) return;
    updatePreferences.mutate({ ...preferences, defaultLandingPage });
  }

  if (isLoading) {
    return <p>Loading...</p>;
  }
  if (isError || !preferences) {
    return <p>Dashboard settings could not be loaded.</p>;
  }

  return (
    <div className="settings-form">
      <div className="settings-form__field">
        <label>Default landing page</label>
        <p className="settings-form__hint">Where you land after signing in.</p>
        {LANDING_OPTIONS.map((option) => (
          <label key={option.id} className="settings-form__radio">
            <input
              type="radio"
              name="default-landing-page"
              checked={preferences.defaultLandingPage === option.id}
              onChange={() => setLandingPage(option.id)}
              disabled={updatePreferences.isPending}
            />
            <span>
              <strong>{option.label}</strong>
              <span className="settings-form__radio-description">{option.description}</span>
            </span>
          </label>
        ))}
      </div>

      <p className="settings-form__hint">
        Widget visibility and order are customized directly on the <Link to="/dashboard">Dashboard</Link> itself — look for
        &ldquo;Customize Dashboard&rdquo;.
      </p>

      {updatePreferences.isError && (
        <p className="form-error">
          {updatePreferences.error instanceof ApiError ? updatePreferences.error.message : 'Could not save dashboard settings.'}
        </p>
      )}
    </div>
  );
}
