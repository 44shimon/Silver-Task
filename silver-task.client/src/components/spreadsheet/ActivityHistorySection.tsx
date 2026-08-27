import { useTaskActivities } from '@/hooks/useTaskActivities';
import { describeActivity } from '@/utils/describeActivity';
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
