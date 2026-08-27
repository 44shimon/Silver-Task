import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Activity } from 'lucide-react';
import type { ActivityFeedItem } from '@/types/dashboard';
import type { TaskActivity } from '@/types/activity';
import { describeActivity } from '@/utils/describeActivity';
import { useRecentActivity } from '@/hooks/useDashboard';
import { useCurrentUser } from '@/hooks/useAuth';
import { DashboardWidget } from './DashboardWidget';
import { formatRelativeTime } from '@/utils/formatDate';
import './RecentActivityWidget.css';

// Reuses TaskActivity's own describeActivity formatter (ActivityHistorySection) — the dashboard
// feed just adapts each ActivityFeedItem into that same shape rather than duplicating the
// "what happened" phrasing logic for a second time.
function toTaskActivity(item: ActivityFeedItem): TaskActivity {
  return {
    id: item.id,
    user: item.userName ? { id: '', name: item.userName, email: '', isActive: true } : null,
    action: item.action,
    fieldName: item.fieldName,
    oldValue: item.oldValue,
    newValue: item.newValue,
    createdAt: item.createdAt,
  };
}

export function RecentActivityWidget() {
  const [mineOnly, setMineOnly] = useState(false);
  const { data: currentUser } = useCurrentUser();
  const { data: activity, isLoading, isError, refetch } = useRecentActivity(mineOnly);
  const navigate = useNavigate();

  return (
    <DashboardWidget
      title={mineOnly ? 'My Activity' : 'Recent Activity'}
      icon={<Activity size={14} />}
      isLoading={isLoading}
      isError={isError}
      onRetry={() => refetch()}
      isEmpty={(activity?.length ?? 0) === 0}
      emptyTitle="No recent activity"
      headerAction={
        currentUser && (
          <button type="button" className="recent-activity-widget__toggle" onClick={() => setMineOnly((v) => !v)}>
            {mineOnly ? 'Show all' : 'Show mine'}
          </button>
        )
      }
    >
      <ul className="recent-activity-widget">
        {activity?.map((item) => (
          <li key={item.id}>
            <button
              type="button"
              className="recent-activity-widget__row"
              onClick={() => navigate(`/projects/${item.projectId}?task=${item.taskId}`)}
            >
              <span className="recent-activity-widget__text">
                {describeActivity(toTaskActivity(item))} on <strong>{item.taskTitle}</strong>
              </span>
              <span className="recent-activity-widget__meta">
                {item.projectName} · {formatRelativeTime(item.createdAt)}
              </span>
            </button>
          </li>
        ))}
      </ul>
    </DashboardWidget>
  );
}
