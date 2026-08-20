import { STATUS_LABELS, type TaskStatus } from '@/types/task';
import type { TaskActivity } from '@/types/activity';
import { useTaskActivities } from '@/hooks/useTaskActivities';
import { formatDate } from '@/utils/formatDate';
import './ActivityHistorySection.css';

interface ActivityHistorySectionProps {
  taskId: string;
}

export function ActivityHistorySection({ taskId }: ActivityHistorySectionProps) {
  const { data: activities } = useTaskActivities(taskId);

  return (
    <div className="task-detail-panel__section">
      <h3>Activity History</h3>
      <div className="activity-list">
        {activities?.map((activity) => (
          <div className="activity-row" key={activity.id}>
            <span className="activity-row__text">{describeActivity(activity)}</span>
            <span className="activity-row__date">{new Date(activity.createdAt).toLocaleString()}</span>
          </div>
        ))}
        {activities?.length === 0 && <p className="activity-list__empty">No activity yet.</p>}
      </div>
    </div>
  );
}

function describeActivity(activity: TaskActivity): string {
  const actor = activity.user?.name ?? 'Someone';

  if (activity.action === 'Created') {
    return `${actor} created this task`;
  }

  if (activity.action === 'Assigned') {
    return activity.newValue
      ? `${actor} assigned this task to ${activity.newValue}`
      : `${actor} unassigned this task`;
  }

  if (activity.action === 'AttachmentAdded') {
    return `${actor} attached ${activity.newValue ?? 'a file'}`;
  }

  if (activity.action === 'AttachmentRemoved') {
    return `${actor} removed attachment ${activity.oldValue ?? ''}`.trim();
  }

  const field = activity.fieldName ?? 'a field';
  const oldDisplay = formatActivityValue(activity.fieldName, activity.oldValue);
  const newDisplay = formatActivityValue(activity.fieldName, activity.newValue);
  return `${actor} changed ${field} from ${oldDisplay} to ${newDisplay}`;
}

function formatActivityValue(fieldName: string | null, value: string | null): string {
  if (!value) {
    return '(none)';
  }
  if (fieldName === 'Status') {
    return STATUS_LABELS[value as TaskStatus] ?? value;
  }
  if (fieldName === 'Start Date' || fieldName === 'Due Date') {
    return formatDate(value);
  }
  return value;
}
