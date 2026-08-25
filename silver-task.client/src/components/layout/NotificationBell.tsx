import { useRef } from 'react';
import { Link } from 'react-router-dom';
import { Bell } from 'lucide-react';
import { useMarkAllNotificationsRead, useNotifications, useUnreadCount } from '@/hooks/useNotifications';
import { NotificationItem } from './NotificationItem';
import './NotificationBell.css';

const DROPDOWN_PAGE_SIZE = 10;

// Same <details>-based popover pattern as UserMenu/CustomFieldsPanel — anchored in the header,
// not a grid toolbar, but the same interaction (click to open, click outside/an item to close).
export function NotificationBell() {
  const { data: unread } = useUnreadCount();
  const { data: recent, isLoading, isError, refetch } = useNotifications({ page: 1, pageSize: DROPDOWN_PAGE_SIZE });
  const markAllRead = useMarkAllNotificationsRead();
  const detailsRef = useRef<HTMLDetailsElement>(null);

  const count = unread?.count ?? 0;
  const badgeLabel = count > 99 ? '99+' : String(count);

  function closeMenu() {
    if (detailsRef.current) {
      detailsRef.current.open = false;
    }
  }

  return (
    <details className="notification-bell" ref={detailsRef}>
      <summary className="icon-button notification-bell__trigger" aria-label="Notifications">
        <Bell size={18} />
        {count > 0 && <span className="notification-bell__badge">{badgeLabel}</span>}
      </summary>

      <div className="notification-bell__panel">
        <div className="notification-bell__header">
          <span>Notifications</span>
          {count > 0 && (
            <button type="button" onClick={() => markAllRead.mutate()} disabled={markAllRead.isPending}>
              Mark all read
            </button>
          )}
        </div>

        <div className="notification-bell__list">
          {isLoading && <p className="notification-bell__status">Loading...</p>}
          {isError && (
            <div className="notification-bell__status">
              <p>Could not load notifications.</p>
              <button type="button" onClick={() => refetch()}>
                Retry
              </button>
            </div>
          )}
          {!isLoading && !isError && recent?.items.length === 0 && (
            <p className="notification-bell__status">No new notifications</p>
          )}
          {!isLoading &&
            !isError &&
            recent?.items.map((notification) => (
              <NotificationItem key={notification.id} notification={notification} onNavigate={closeMenu} />
            ))}
        </div>

        <Link to="/notifications" className="notification-bell__footer" onClick={closeMenu}>
          View all notifications
        </Link>
      </div>
    </details>
  );
}
