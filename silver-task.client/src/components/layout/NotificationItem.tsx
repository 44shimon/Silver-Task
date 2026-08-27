import { useNavigate } from 'react-router-dom';
import { AtSign, Check, ClipboardList, File, Folder, MessageSquare, RotateCcw, Settings, X, Zap, type LucideIcon } from 'lucide-react';
import type { AppNotification } from '@/types/notification';
import { categoryOf } from '@/types/notification';
import { useDeleteNotification, useMarkNotificationRead, useMarkNotificationUnread } from '@/hooks/useNotifications';
import { formatRelativeTime } from '@/utils/formatDate';
import './NotificationItem.css';

interface NotificationItemProps {
  notification: AppNotification;
  /** Dropdown-only — closes the popover after navigating away. */
  onNavigate?: () => void;
  /** Notification center only — renders a checkbox for the bulk-select toolbar. */
  selectable?: boolean;
  selected?: boolean;
  onToggleSelected?: (id: string) => void;
}

// Mirrors NotificationCategory (types/notification.ts) — one icon per notification-center tab,
// reusing this app's existing icon library (lucide-react), not a new icon framework.
const CATEGORY_ICONS: Record<string, LucideIcon> = {
  tasks: ClipboardList,
  mentions: AtSign,
  projects: Folder,
  files: File,
  automations: Zap,
  system: Settings,
};

function CategoryIcon({ type }: { type: string }) {
  // A comment notification is filed under the "tasks" category but reads more naturally with
  // its own icon — checked before the category lookup rather than adding a "comments" category
  // that would otherwise duplicate "tasks" everywhere else (filters, tabs, settings groups).
  const Icon = type === 'CommentAdded' ? MessageSquare : (CATEGORY_ICONS[categoryOf(type)] ?? Settings);
  return <Icon size={14} />;
}

// Shared between the header dropdown and the full /notifications page, per the "do not create
// another task detail implementation" spirit applied to notification rendering too — one row
// component, reused, rather than near-duplicates in two places.
export function NotificationItem({ notification, onNavigate, selectable, selected, onToggleSelected }: NotificationItemProps) {
  const navigate = useNavigate();
  const markRead = useMarkNotificationRead();
  const markUnread = useMarkNotificationUnread();
  const deleteNotification = useDeleteNotification();

  const hasDestination = !!notification.actionUrl;
  // A handful of types (e.g. a system-wide role change) never have a task/project destination
  // by design — only tasks/projects/files/mentions/comments normally do, so the "no longer
  // available" hint is scoped to those categories to avoid mislabeling the former as deleted.
  const category = categoryOf(notification.type);
  const expectsDestination = category === 'tasks' || category === 'projects' || category === 'files' || category === 'mentions';

  function handleOpen() {
    if (!notification.isRead) {
      markRead.mutate(notification.id);
    }
    // The destination route (opened via the existing ?task=<id> convention, or a plain project
    // route) always re-enforces its own authorization on load — a stale/inaccessible link can
    // never expose more than that route already allows a direct navigation to see.
    if (notification.actionUrl) {
      navigate(notification.actionUrl);
    }
    onNavigate?.();
  }

  return (
    <div className={`notification-item${notification.isRead ? '' : ' notification-item--unread'}`}>
      {selectable && (
        <input
          type="checkbox"
          className="notification-item__checkbox"
          checked={!!selected}
          onChange={() => onToggleSelected?.(notification.id)}
          aria-label={`Select notification: ${notification.title}`}
        />
      )}

      <span className={`notification-item__priority-dot notification-item__priority-dot--${notification.priority.toLowerCase()}`} aria-hidden="true" />

      <button type="button" className="notification-item__main" onClick={handleOpen} disabled={!hasDestination && notification.isRead}>
        <span className="notification-item__title-row">
          <CategoryIcon type={notification.type} />
          <span className="notification-item__title">{notification.title}</span>
        </span>
        <span className="notification-item__message">
          {notification.message}
          {!hasDestination && expectsDestination && <span className="notification-item__unavailable"> (no longer available)</span>}
        </span>
        <span className="notification-item__time">{formatRelativeTime(notification.createdAt)}</span>
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
          aria-label="Dismiss notification"
          title="Dismiss notification"
          disabled={deleteNotification.isPending}
          onClick={() => deleteNotification.mutate({ id: notification.id, wasUnread: !notification.isRead })}
        >
          <X size={13} />
        </button>
      </div>
    </div>
  );
}
