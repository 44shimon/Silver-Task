import { Link } from 'react-router-dom';
import { Bell } from 'lucide-react';
import { useNotifications, useUnreadCount } from '@/hooks/useNotifications';
import { NotificationItem } from '@/components/layout/NotificationItem';
import { DashboardWidget } from './DashboardWidget';
import './NotificationsWidget.css';

// Reuses the Notification Center's own hooks/row component (Phase 36) end to end — no
// duplicated notification logic, per the spec's own "reuse the Notification Center" instruction.
export function NotificationsWidget() {
  const { data: unread } = useUnreadCount();
  const { data, isLoading, isError, refetch } = useNotifications({ page: 1, pageSize: 5 });

  return (
    <DashboardWidget
      title="Notifications"
      icon={<Bell size={14} />}
      isLoading={isLoading}
      isError={isError}
      onRetry={() => refetch()}
      isEmpty={data?.items.length === 0}
      emptyTitle="No new notifications"
      headerAction={unread?.count ? <span className="notifications-widget__count">{unread.count} unread</span> : undefined}
    >
      <div className="notifications-widget__list">
        {data?.items.map((notification) => (
          <NotificationItem key={notification.id} notification={notification} />
        ))}
      </div>
      <Link to="/notifications" className="notifications-widget__view-all">
        View all
      </Link>
    </DashboardWidget>
  );
}
