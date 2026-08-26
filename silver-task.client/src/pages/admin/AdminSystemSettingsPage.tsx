import { useState, type FormEvent } from 'react';
import { CheckCircle2, XCircle } from 'lucide-react';
import { useSystemSettings, useUpdateSystemSettings } from '@/hooks/useSystemSettings';
import { useStorageHealth } from '@/hooks/useAdminStats';
import type { SystemSetting, SystemSettingSection } from '@/types/settings';
import { PRIORITY_OPTIONS, STATUS_LABELS, STATUS_OPTIONS } from '@/types/task';
import { ApiError } from '@/api/httpClient';
import { formatFileSize } from '@/utils/formatFileSize';
import '../settings/SettingsForm.css';
import './AdminSystemSettingsPage.css';

type FieldKind = 'text' | 'number' | 'boolean' | 'select';

interface FieldConfig {
  key: string;
  title: string;
  kind: FieldKind;
  options?: { value: string; label: string }[];
  min?: number;
  max?: number;
}

// Matches UserPreferencesService's server-side date-format allow-list exactly (the same set
// PreferencesSettingsPage offers for a user's own preference).
const DATE_FORMAT_OPTIONS = ['MM/dd/yyyy', 'dd/MM/yyyy', 'yyyy-MM-dd', 'dd MMM yyyy'];
const TIME_ZONE_OPTIONS: string[] =
  typeof Intl.supportedValuesOf === 'function' ? Intl.supportedValuesOf('timeZone') : ['UTC'];

// One entry per key in Common/SystemSettingKeys.cs — bounds (min/max) mirror
// SystemSettingsService.ValidateIntBounds exactly, purely for input UX; the server re-validates
// independently regardless of what these attributes allow through.
const SECTIONS: { id: SystemSettingSection; title: string; fields: FieldConfig[] }[] = [
  {
    id: 'General',
    title: 'General',
    fields: [
      { key: 'General.ApplicationName', title: 'Application name', kind: 'text' },
      { key: 'General.ApplicationDescription', title: 'Application description', kind: 'text' },
      {
        key: 'General.DefaultTimeZone',
        title: 'Default time zone',
        kind: 'select',
        options: TIME_ZONE_OPTIONS.map((zone) => ({ value: zone, label: zone })),
      },
      {
        key: 'General.DefaultDateFormat',
        title: 'Default date format',
        kind: 'select',
        options: DATE_FORMAT_OPTIONS.map((format) => ({ value: format, label: format })),
      },
      {
        key: 'General.DefaultTimeFormat',
        title: 'Default time format',
        kind: 'select',
        options: [
          { value: '12h', label: '12-hour' },
          { value: '24h', label: '24-hour' },
        ],
      },
      { key: 'General.DefaultItemsPerPage', title: 'Default items per page', kind: 'number', min: 5, max: 200 },
    ],
  },
  {
    id: 'TaskDefaults',
    title: 'Task Defaults',
    fields: [
      {
        key: 'TaskDefaults.DefaultStatus',
        title: 'Default task status',
        kind: 'select',
        options: STATUS_OPTIONS.map((status) => ({ value: status, label: STATUS_LABELS[status] })),
      },
      {
        key: 'TaskDefaults.DefaultPriority',
        title: 'Default task priority',
        kind: 'select',
        options: PRIORITY_OPTIONS.map((priority) => ({ value: priority, label: priority })),
      },
    ],
  },
  {
    id: 'ProjectDefaults',
    title: 'Project Defaults',
    fields: [{ key: 'ProjectDefaults.RequireDescription', title: 'Require a description when creating a project', kind: 'boolean' }],
  },
  {
    id: 'Security',
    title: 'Security',
    fields: [
      { key: 'Security.SessionTimeoutMinutes', title: 'Session timeout (minutes)', kind: 'number', min: 5, max: 43200 },
      { key: 'Security.MinPasswordLength', title: 'Minimum password length', kind: 'number', min: 6, max: 128 },
      { key: 'Security.RequirePasswordComplexity', title: 'Require password complexity', kind: 'boolean' },
      { key: 'Security.MaxFailedLoginAttempts', title: 'Max failed login attempts', kind: 'number', min: 3, max: 20 },
      { key: 'Security.AccountLockoutDurationMinutes', title: 'Account lockout duration (minutes)', kind: 'number', min: 1, max: 1440 },
    ],
  },
  {
    id: 'Behavior',
    title: 'System Behavior',
    fields: [
      { key: 'Behavior.AllowUsersToCreateProjects', title: 'Allow users to create projects', kind: 'boolean' },
      { key: 'Behavior.AllowMembersToCreateTasks', title: 'Allow members to create tasks', kind: 'boolean' },
      { key: 'Behavior.AllowMembersToDeleteTasks', title: 'Allow members to delete tasks', kind: 'boolean' },
      { key: 'Behavior.AllowUsersToCreateCustomFields', title: 'Allow members to create custom fields', kind: 'boolean' },
      { key: 'Behavior.AllowComments', title: 'Allow comments', kind: 'boolean' },
      { key: 'Behavior.AllowAttachments', title: 'Allow attachments', kind: 'boolean' },
    ],
  },
  {
    id: 'Attachments',
    title: 'Attachments',
    fields: [
      { key: 'Attachments.MaxSizeMb', title: 'Maximum file size (MB)', kind: 'number', min: 1, max: 500 },
      { key: 'Attachments.AllowedExtensions', title: 'Allowed file extensions (comma-separated)', kind: 'text' },
    ],
  },
];

export function AdminSystemSettingsPage() {
  const { data: settings } = useSystemSettings();
  const { data: storageHealth } = useStorageHealth();
  const updateSettings = useUpdateSystemSettings();

  const [form, setForm] = useState<Record<string, string> | null>(null);
  const [justSaved, setJustSaved] = useState(false);
  // Same "adjust state during render" seeding pattern as PreferencesSettingsPage — sync once
  // when the query result first arrives, not via a useEffect that just costs an extra render.
  const [hasLoadedOnce, setHasLoadedOnce] = useState(false);
  if (settings && !hasLoadedOnce) {
    setHasLoadedOnce(true);
    setForm(Object.fromEntries(settings.map((s) => [s.key, s.value])));
  }

  const byKey: Record<string, SystemSetting> = Object.fromEntries((settings ?? []).map((s) => [s.key, s]));

  function update(key: string, value: string) {
    setForm((prev) => (prev ? { ...prev, [key]: value } : prev));
    setJustSaved(false);
  }

  function handleSubmit(event: FormEvent) {
    event.preventDefault();
    if (!form) {
      return;
    }
    updateSettings.mutate(form, { onSuccess: () => setJustSaved(true) });
  }

  if (!form) {
    return <p>Loading...</p>;
  }

  return (
    <form className="admin-system-settings" onSubmit={handleSubmit}>
      {SECTIONS.map((section) => (
        <div className="admin-system-settings__section" key={section.id}>
          <h2 className="admin-system-settings__section-title">{section.title}</h2>
          <div className="settings-form">
            {section.fields.map((field) => (
              <SettingField
                key={field.key}
                field={field}
                value={form[field.key] ?? ''}
                description={byKey[field.key]?.description ?? null}
                disabled={updateSettings.isPending}
                onChange={(value) => update(field.key, value)}
              />
            ))}
          </div>
          {section.id === 'Attachments' && storageHealth && (
            <div className="admin-system-settings__storage-health">
              {storageHealth.isWritable ? (
                <CheckCircle2 size={14} className="admin-system-settings__storage-ok" />
              ) : (
                <XCircle size={14} className="admin-system-settings__storage-bad" />
              )}
              <span>
                Storage: {storageHealth.isWritable ? 'Connected' : 'Not writable'} ({storageHealth.provider}) &middot;{' '}
                {storageHealth.fileCount} file{storageHealth.fileCount === 1 ? '' : 's'} &middot;{' '}
                {formatFileSize(storageHealth.totalBytes)} used
              </span>
            </div>
          )}
        </div>
      ))}

      {updateSettings.isError && (
        <p className="form-error">
          {updateSettings.error instanceof ApiError ? updateSettings.error.message : 'Could not save system settings.'}
        </p>
      )}
      {justSaved && !updateSettings.isError && <p className="settings-form__success">System settings saved.</p>}

      <button type="submit" className="settings-form__save" disabled={updateSettings.isPending}>
        {updateSettings.isPending ? 'Saving...' : 'Save changes'}
      </button>
    </form>
  );
}

function SettingField({
  field,
  value,
  description,
  disabled,
  onChange,
}: {
  field: FieldConfig;
  value: string;
  description: string | null;
  disabled: boolean;
  onChange: (value: string) => void;
}) {
  if (field.kind === 'boolean') {
    const isOn = value === 'true';
    return (
      <div className="settings-toggle-row">
        <div className="settings-toggle-row__label">
          <span className="settings-toggle-row__title">{field.title}</span>
          {description && <span className="settings-toggle-row__description">{description}</span>}
        </div>
        <button
          type="button"
          className={`settings-toggle${isOn ? ' settings-toggle--on' : ''}`}
          role="switch"
          aria-checked={isOn}
          aria-label={field.title}
          disabled={disabled}
          onClick={() => onChange(isOn ? 'false' : 'true')}
        />
      </div>
    );
  }

  return (
    <div className="settings-form__field">
      <label htmlFor={field.key}>{field.title}</label>
      {field.kind === 'select' ? (
        <select id={field.key} value={value} disabled={disabled} onChange={(e) => onChange(e.target.value)}>
          {field.options?.map((option) => (
            <option key={option.value} value={option.value}>
              {option.label}
            </option>
          ))}
        </select>
      ) : (
        <input
          id={field.key}
          type={field.kind === 'number' ? 'number' : 'text'}
          min={field.min}
          max={field.max}
          value={value}
          disabled={disabled}
          onChange={(e) => onChange(e.target.value)}
        />
      )}
      {description && <span className="settings-form__readonly-label">{description}</span>}
    </div>
  );
}
