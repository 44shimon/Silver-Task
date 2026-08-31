import { useState } from 'react';
import {
  useNotificationSettings,
  useUpdateNotificationSettings,
  useUpdatePreferences,
  useUserPreferences,
} from '@/hooks/useUserSettings';
import type { DigestFrequency } from '@/types/settings';
import { ApiError } from '@/api/httpClient';
import './SettingsForm.css';
import './NotificationSettingsPage.css';

// Labels/groups are the only thing hardcoded client-side — the actual set of settings (which
// types exist, and each one's current value) comes entirely from the API response
// (NotificationTypes.All server-side), so a new type shows up here automatically once the
// backend knows about it; only its label/group would need adding.
const LABELS: Record<string, string> = {
  TaskAssigned: 'Task assigned to me',
  TaskReassigned: 'Task reassigned to me',
  TaskUnassigned: 'Removed from a task',
  TaskStatusChanged: 'Task status changed',
  TaskPriorityChanged: 'Task priority changed',
  TaskDueDateChanged: 'Task due date changed',
  TaskDueSoon: 'Task due soon',
  TaskOverdue: 'Task overdue',
  TaskCompleted: 'Task completed',
  TaskReopened: 'Task reopened',
  TaskDependencyCompleted: 'Task dependency completed',
  RecurringTaskAssigneeInactive: 'Recurring task needs attention',
  CommentAdded: 'Comments on my tasks',
  MentionedInComment: 'Someone mentions me',
  ProjectTaskCompleted: 'Task completed in my project',
  FileUploaded: 'Files uploaded to my tasks/projects',
  UserAddedToProject: 'Added to a project',
  UserRemovedFromProject: 'Removed from a project',
  ProjectStatusChanged: 'Project archived/restored',
  ProjectRoleChanged: 'My project role changed',
  SystemRoleChanged: 'My system role changed',
  AutomationNotification: 'Automation notifications',
};

const GROUPS: { title: string; types: string[] }[] = [
  {
    title: 'Tasks',
    types: [
      'TaskAssigned', 'TaskReassigned', 'TaskUnassigned', 'TaskStatusChanged', 'TaskPriorityChanged',
      'TaskDueDateChanged', 'TaskDueSoon', 'TaskOverdue', 'TaskCompleted', 'TaskReopened',
      'TaskDependencyCompleted', 'RecurringTaskAssigneeInactive',
    ],
  },
  { title: 'Comments', types: ['CommentAdded', 'MentionedInComment'] },
  { title: 'Files', types: ['FileUploaded'] },
  { title: 'Projects', types: ['UserAddedToProject', 'UserRemovedFromProject', 'ProjectStatusChanged', 'ProjectRoleChanged', 'ProjectTaskCompleted'] },
  { title: 'Automations', types: ['AutomationNotification'] },
  { title: 'System', types: ['SystemRoleChanged'] },
];

export function NotificationSettingsPage() {
  const { data: settings, isLoading, isError } = useNotificationSettings();
  const updateSettings = useUpdateNotificationSettings();
  const { data: preferences } = useUserPreferences();
  const updatePreferences = useUpdatePreferences();

  const [digestFrequency, setDigestFrequency] = useState<DigestFrequency>('Immediately');
  const [quietHoursEnabled, setQuietHoursEnabled] = useState(false);
  const [quietHoursStart, setQuietHoursStart] = useState('20:00');
  const [quietHoursEnd, setQuietHoursEnd] = useState('07:00');
  // Same "adjust state during render" seeding pattern as PreferencesSettingsPage — sync once
  // when the query result first arrives, without an effect that just triggers a second render.
  const [hasLoadedPreferences, setHasLoadedPreferences] = useState(false);
  if (preferences && !hasLoadedPreferences) {
    setHasLoadedPreferences(true);
    setDigestFrequency(preferences.digestFrequency);
    setQuietHoursEnabled(preferences.quietHoursEnabled);
    setQuietHoursStart(preferences.quietHoursStart?.slice(0, 5) ?? '20:00');
    setQuietHoursEnd(preferences.quietHoursEnd?.slice(0, 5) ?? '07:00');
  }

  function toggle(notificationType: string, channel: 'inAppEnabled' | 'emailEnabled', value: boolean) {
    const current = settings?.find((s) => s.notificationType === notificationType);
    if (!current) return;
    updateSettings.mutate([{ ...current, [channel]: value }]);
  }

  // The single master email switch (Phase 45) — same "mutate the full preferences object
  // immediately" pattern as the per-type toggles above, rather than requiring the separate
  // "Save" click the digest/quiet-hours section below uses, since this is the one toggle users
  // most expect to take effect the instant they flip it.
  function toggleEmailNotificationsEnabled(value: boolean) {
    if (!preferences) return;
    updatePreferences.mutate({ ...preferences, emailNotificationsEnabled: value });
  }

  function saveDigestAndQuietHours() {
    if (!preferences) return;
    updatePreferences.mutate({
      ...preferences,
      digestFrequency,
      quietHoursEnabled,
      quietHoursStart: quietHoursEnabled ? `${quietHoursStart}:00` : null,
      quietHoursEnd: quietHoursEnabled ? `${quietHoursEnd}:00` : null,
    });
  }

  if (isLoading) {
    return <p>Loading...</p>;
  }
  if (isError || !settings) {
    return <p>Notification settings could not be loaded.</p>;
  }

  return (
    <div className="notification-settings-page">
      <div className="notification-settings-page__group">
        <div className="settings-toggle-row">
          <div className="settings-toggle-row__label">
            <span className="settings-toggle-row__title">Email notifications</span>
            <span className="settings-toggle-row__description">
              Master switch for all outgoing notification emails. Turning this off stops every email below
              regardless of its individual setting; in-app notifications are unaffected.
            </span>
          </div>
          <button
            type="button"
            className={`settings-toggle${preferences?.emailNotificationsEnabled ? ' settings-toggle--on' : ''}`}
            role="switch"
            aria-checked={preferences?.emailNotificationsEnabled ?? true}
            aria-label="Email notifications"
            disabled={!preferences || updatePreferences.isPending}
            onClick={() => toggleEmailNotificationsEnabled(!preferences?.emailNotificationsEnabled)}
          />
        </div>
      </div>

      {GROUPS.map((group) => (
        <div className="notification-settings-page__group" key={group.title}>
          <h3>{group.title}</h3>
          <div className="notification-settings-page__grid">
            <div className="notification-settings-page__grid-header">
              <span />
              <span>In-App</span>
              <span>Email</span>
            </div>
            {group.types.map((type) => {
              const setting = settings.find((s) => s.notificationType === type);
              if (!setting) return null;
              return (
                <div className="notification-settings-page__grid-row" key={type}>
                  <span className="notification-settings-page__grid-label">{LABELS[type] ?? type}</span>
                  <button
                    type="button"
                    className={`settings-toggle${setting.inAppEnabled ? ' settings-toggle--on' : ''}`}
                    role="switch"
                    aria-checked={setting.inAppEnabled}
                    aria-label={`${LABELS[type] ?? type} — in-app`}
                    disabled={updateSettings.isPending}
                    onClick={() => toggle(type, 'inAppEnabled', !setting.inAppEnabled)}
                  />
                  <button
                    type="button"
                    className={`settings-toggle${setting.emailEnabled ? ' settings-toggle--on' : ''}`}
                    role="switch"
                    aria-checked={setting.emailEnabled}
                    aria-label={`${LABELS[type] ?? type} — email`}
                    disabled={updateSettings.isPending}
                    onClick={() => toggle(type, 'emailEnabled', !setting.emailEnabled)}
                  />
                </div>
              );
            })}
          </div>
        </div>
      ))}

      {updateSettings.isError && (
        <p className="form-error">
          {updateSettings.error instanceof ApiError ? updateSettings.error.message : 'Could not save notification settings.'}
        </p>
      )}

      <div className="notification-settings-page__group">
        <h3>Email Digest &amp; Quiet Hours</h3>
        <div className="settings-form__field">
          <label>Email digest frequency</label>
          <select value={digestFrequency} onChange={(e) => setDigestFrequency(e.target.value as DigestFrequency)}>
            <option value="Immediately">Immediately — email as each notification happens</option>
            <option value="Daily">Daily — one summary email per day</option>
            <option value="Never">Never — no notification emails</option>
          </select>
        </div>

        <div className="settings-toggle-row">
          <div className="settings-toggle-row__label">
            <span className="settings-toggle-row__title">Quiet hours</span>
            <span className="settings-toggle-row__description">Suppress notification emails during this window (in-app notifications are always still saved).</span>
          </div>
          <button
            type="button"
            className={`settings-toggle${quietHoursEnabled ? ' settings-toggle--on' : ''}`}
            role="switch"
            aria-checked={quietHoursEnabled}
            aria-label="Quiet hours"
            onClick={() => setQuietHoursEnabled((v) => !v)}
          />
        </div>

        {quietHoursEnabled && (
          <div className="notification-settings-page__quiet-hours-row">
            <div className="settings-form__field">
              <label>From</label>
              <input type="time" value={quietHoursStart} onChange={(e) => setQuietHoursStart(e.target.value)} />
            </div>
            <div className="settings-form__field">
              <label>To</label>
              <input type="time" value={quietHoursEnd} onChange={(e) => setQuietHoursEnd(e.target.value)} />
            </div>
          </div>
        )}

        <button type="button" className="settings-form__save" onClick={saveDigestAndQuietHours} disabled={updatePreferences.isPending || !preferences}>
          {updatePreferences.isPending ? 'Saving...' : 'Save'}
        </button>
        {updatePreferences.isError && (
          <p className="form-error">
            {updatePreferences.error instanceof ApiError ? updatePreferences.error.message : 'Could not save digest/quiet hours settings.'}
          </p>
        )}
      </div>
    </div>
  );
}
