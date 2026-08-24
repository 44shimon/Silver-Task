import { useNotificationSettings, useUpdateNotificationSettings } from '@/hooks/useUserSettings';
import { ApiError } from '@/api/httpClient';
import './SettingsForm.css';

// Labels are the only thing hardcoded client-side — the actual set of settings (which types
// exist, and each one's current value) comes entirely from the API response
// (NotificationTypes.All server-side), so a new type shows up here automatically once the
// backend knows about it; only its label would need adding.
const LABELS: Record<string, { title: string; description: string }> = {
  TaskAssigned: { title: 'Task assigned to me', description: 'When someone assigns a task to you.' },
  TaskStatusChanged: { title: 'Task status changed', description: 'When a task you’re involved in changes status.' },
  TaskDueSoon: { title: 'Task due soon', description: 'A reminder shortly before a task’s due date.' },
  TaskOverdue: { title: 'Task overdue', description: 'When a task assigned to you passes its due date.' },
  CommentAdded: { title: 'Comment added to my task', description: 'When someone comments on a task assigned to you.' },
  MentionedInComment: { title: 'Mentioned in a comment', description: 'When someone mentions you in a comment.' },
};

export function NotificationSettingsPage() {
  const { data: settings, isLoading, isError } = useNotificationSettings();
  const updateSettings = useUpdateNotificationSettings();

  function toggle(notificationType: string, isEnabled: boolean) {
    updateSettings.mutate([{ notificationType, isEnabled }]);
  }

  if (isLoading) {
    return <p>Loading...</p>;
  }
  if (isError || !settings) {
    return <p>Notification settings could not be loaded.</p>;
  }

  return (
    <div className="settings-form">
      {settings.map((setting) => {
        const label = LABELS[setting.notificationType] ?? { title: setting.notificationType, description: '' };
        return (
          <div className="settings-toggle-row" key={setting.notificationType}>
            <div className="settings-toggle-row__label">
              <span className="settings-toggle-row__title">{label.title}</span>
              {label.description && <span className="settings-toggle-row__description">{label.description}</span>}
            </div>
            <button
              type="button"
              className={`settings-toggle${setting.isEnabled ? ' settings-toggle--on' : ''}`}
              role="switch"
              aria-checked={setting.isEnabled}
              aria-label={label.title}
              disabled={updateSettings.isPending}
              onClick={() => toggle(setting.notificationType, !setting.isEnabled)}
            />
          </div>
        );
      })}

      {updateSettings.isError && (
        <p className="form-error">
          {updateSettings.error instanceof ApiError ? updateSettings.error.message : 'Could not save notification settings.'}
        </p>
      )}
    </div>
  );
}
