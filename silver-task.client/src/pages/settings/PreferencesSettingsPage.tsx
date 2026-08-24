import { useMemo, useState, type FormEvent } from 'react';
import { useProjects } from '@/hooks/useProjects';
import { useUpdatePreferences, useUserPreferences } from '@/hooks/useUserSettings';
import type { Theme, TimeFormat, UpdatePreferencesRequest } from '@/types/settings';
import { ApiError } from '@/api/httpClient';
import './SettingsForm.css';

const THEME_OPTIONS: Theme[] = ['System', 'Light', 'Dark'];
const TIME_FORMAT_OPTIONS: TimeFormat[] = ['12h', '24h'];
// Matches UserPreferencesService's server-side allow-list exactly — the server is still the
// authority (it re-validates independently), this just keeps the picker from offering values
// that would only bounce back as an error.
const DATE_FORMAT_OPTIONS = ['MM/dd/yyyy', 'dd/MM/yyyy', 'yyyy-MM-dd', 'dd MMM yyyy'];
const TASK_VIEW_OPTIONS: { id: string; label: string }[] = [
  { id: 'table', label: 'Table' },
  { id: 'kanban', label: 'Kanban' },
  { id: 'calendar', label: 'Calendar' },
  { id: 'timeline', label: 'Timeline' },
  { id: 'gantt', label: 'Gantt' },
];

// Intl.supportedValuesOf('timeZone') gives the browser's full IANA time zone list — the same
// identifiers .NET's TimeZoneInfo.FindSystemTimeZoneById validates against server-side, so
// nothing offered here can fail that check.
const TIME_ZONE_OPTIONS: string[] =
  typeof Intl.supportedValuesOf === 'function' ? Intl.supportedValuesOf('timeZone') : ['UTC'];

export function PreferencesSettingsPage() {
  const { data: preferences } = useUserPreferences();
  const { data: projects } = useProjects();
  const updatePreferences = useUpdatePreferences();

  const [form, setForm] = useState<UpdatePreferencesRequest | null>(null);
  const [justSaved, setJustSaved] = useState(false);
  // Same "adjust state during render" seeding pattern as ProfileSettingsPage — sync once when
  // the query result first arrives, without a useEffect that just triggers a second render.
  const [hasLoadedOnce, setHasLoadedOnce] = useState(false);
  if (preferences && !hasLoadedOnce) {
    setHasLoadedOnce(true);
    setForm(preferences);
  }

  const projectOptions = useMemo(() => projects ?? [], [projects]);

  function update(patch: Partial<UpdatePreferencesRequest>) {
    setForm((prev) => (prev ? { ...prev, ...patch } : prev));
    setJustSaved(false);
  }

  function handleSubmit(event: FormEvent) {
    event.preventDefault();
    if (!form) {
      return;
    }
    updatePreferences.mutate(form, { onSuccess: () => setJustSaved(true) });
  }

  if (!form) {
    return <p>Loading...</p>;
  }

  return (
    <form className="settings-form" onSubmit={handleSubmit}>
      <div className="settings-form__field">
        <label htmlFor="pref-theme">Theme</label>
        <select id="pref-theme" value={form.theme} onChange={(e) => update({ theme: e.target.value as Theme })}>
          {THEME_OPTIONS.map((option) => (
            <option key={option} value={option}>
              {option}
            </option>
          ))}
        </select>
      </div>

      <div className="settings-form__field">
        <label htmlFor="pref-default-project">Default project</label>
        <select
          id="pref-default-project"
          value={form.defaultProjectId ?? ''}
          onChange={(e) => update({ defaultProjectId: e.target.value || null })}
        >
          <option value="">None</option>
          {projectOptions.map((project) => (
            <option key={project.id} value={project.id}>
              {project.name}
            </option>
          ))}
        </select>
      </div>

      <div className="settings-form__field">
        <label htmlFor="pref-default-view">Default task view</label>
        <select
          id="pref-default-view"
          value={form.defaultTaskView ?? ''}
          onChange={(e) => update({ defaultTaskView: e.target.value || null })}
        >
          <option value="">Table (default)</option>
          {TASK_VIEW_OPTIONS.map((option) => (
            <option key={option.id} value={option.id}>
              {option.label}
            </option>
          ))}
        </select>
      </div>

      <div className="settings-form__field">
        <label htmlFor="pref-date-format">Date format</label>
        <select id="pref-date-format" value={form.dateFormat} onChange={(e) => update({ dateFormat: e.target.value })}>
          {DATE_FORMAT_OPTIONS.map((option) => (
            <option key={option} value={option}>
              {option}
            </option>
          ))}
        </select>
      </div>

      <div className="settings-form__field">
        <label htmlFor="pref-time-format">Time format</label>
        <select
          id="pref-time-format"
          value={form.timeFormat}
          onChange={(e) => update({ timeFormat: e.target.value as TimeFormat })}
        >
          {TIME_FORMAT_OPTIONS.map((option) => (
            <option key={option} value={option}>
              {option === '12h' ? '12-hour' : '24-hour'}
            </option>
          ))}
        </select>
      </div>

      <div className="settings-form__field">
        <label htmlFor="pref-timezone">Time zone</label>
        <select id="pref-timezone" value={form.timeZone} onChange={(e) => update({ timeZone: e.target.value })}>
          {TIME_ZONE_OPTIONS.map((zone) => (
            <option key={zone} value={zone}>
              {zone}
            </option>
          ))}
        </select>
      </div>

      <div className="settings-form__field">
        <label htmlFor="pref-items-per-page">Items per page</label>
        <input
          id="pref-items-per-page"
          type="number"
          min={5}
          max={200}
          value={form.itemsPerPage}
          onChange={(e) => update({ itemsPerPage: Number(e.target.value) })}
        />
      </div>

      {updatePreferences.isError && (
        <p className="form-error">
          {updatePreferences.error instanceof ApiError ? updatePreferences.error.message : 'Could not save preferences.'}
        </p>
      )}
      {justSaved && !updatePreferences.isError && <p className="settings-form__success">Preferences saved.</p>}

      <button type="submit" className="settings-form__save" disabled={updatePreferences.isPending}>
        {updatePreferences.isPending ? 'Saving...' : 'Save changes'}
      </button>
    </form>
  );
}
