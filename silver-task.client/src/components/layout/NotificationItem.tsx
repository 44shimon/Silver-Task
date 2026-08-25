import { useNavigate } from 'react-router-dom';
import { Check, RotateCcw, X } from 'lucide-react';
import type { AppNotification } from '@/types/notification';
import { useDeleteNotification, useMarkNotificationRead, useMarkNotificationUnread } from '@/hooks/useNotifications';
import { formatDateTime } from '@/utils/formatDate';
import './NotificationItem.css';

interface NotificationItemProps {
  notification: AppNotification;
  /** Dropdown-only — closes the popover after navigating away. */
  onNavigate?: () => void;
}

// Shared between the header dropdown and the full /notifications page, per the "do not create
// another task detail implementation" spirit applied to notification rendering too — one row
// component, reused, rather than near-duplicates in two places.
export function NotificationItem({ notification, onNavigate }: NotificationItemProps) {
  const navigate = useNavigate();
  const markRead = useMarkNotificationRead();
  const markUnread = useMarkNotificationUnread();
  const deleteNotification = useDeleteNotification();

  function handleOpen() {
    if (!notification.isRead) {
      markRead.mutate(notification.id);
    }
    // Reuses the exact `?task=<id>` convention TaskDetailPanel/GlobalSearch already use — opens
    // the existing task detail component rather than building a second one.
    if (notification.taskId && notification.projectId) {
      navigate(`/projects/${notification.projectId}?task=${notification.taskId}`);
    } else if (notification.projectId) {
      navigate(`/projects/${notification.projectId}`);
    }
    onNavigate?.();
  }

  return (
    <div className={`notification-item${notification.isRead ? '' : ' notification-item--unread'}`}>
      <button type="button" className="notification-item__main" onClick={handleOpen}>
        <span className="notification-item__title">{notification.title}</span>
        <span className="notification-item__message">{notification.message}</span>
        <span className="notification-item__time">{formatDateTime(notification.createdAt)}</span>
      </button>
      <div className="notification-item__actions">
        <button
          type="button"
          className="icon-button"
          aria-label={notification.isRead ? 'Mark as unread' : 'Mark as read'}
          title={notification.isRead ? 'Mark as unread' : 'Mark as read'}
          disabled={markRead.isPending || markUnread.isPending}
          onClick={() =>
            notification.isRead ? markUnread.mutate(notification.id) : markRead.mutate(notification.id)
          }
        >
          {notification.isRead ? <RotateCcw size={13} /> : <Check size={13} />}
        </button>
        <button
          type="button"
          className="icon-button"
          aria-label="Delete notification"
          title="Delete notification"
          disabled={deleteNotification.isPending}
          onClick={() => deleteNotification.mutate({ id: notification.id, wasUnread: !notification.isRead })}
        >
          <X size={13} />
        </button>
      </div>
    </div>
  );
}
