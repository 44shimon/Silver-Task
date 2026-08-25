import { useState } from 'react';
import { useMarkAllNotificationsRead, useNotifications } from '@/hooks/useNotifications';
import { NotificationItem } from '@/components/layout/NotificationItem';
import './NotificationsPage.css';

type TabFilter = 'all' | 'unread' | 'read';

const PAGE_SIZE = 20;

export function NotificationsPage() {
  const [tab, setTab] = useState<TabFilter>('all');
  const [page, setPage] = useState(1);
  const markAllRead = useMarkAllNotificationsRead();

  const isReadFilter = tab === 'all' ? undefined : tab === 'read';
  const { data, isLoading, isError, refetch } = useNotifications({ isRead: isReadFilter, page, pageSize: PAGE_SIZE });

  function changeTab(next: TabFilter) {
    setTab(next);
    setPage(1);
  }

  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / PAGE_SIZE)) : 1;

  return (
    <div className="notifications-page">
      <div className="notifications-page__header">
        <h1>Notifications</h1>
        <button type="button" className="notifications-page__mark-all" onClick={() => markAllRead.mutate()} disabled={markAllRead.isPending}>
          Mark all read
        </button>
      </div>

      <nav className="notifications-page__tabs" role="tablist">
        {(['all', 'unread', 'read'] as const).map((option) => (
          <button
            key={option}
            type="button"
            className={`notifications-page__tab${tab === option ? ' notifications-page__tab--active' : ''}`}
            onClick={() => changeTab(option)}
          >
            {option === 'all' ? 'All' : option === 'unread' ? 'Unread' : 'Read'}
          </button>
        ))}
      </nav>

      {isLoading && <p>Loading notifications...</p>}

      {isError && (
        <div className="notifications-page__error">
          <p>Notifications could not be loaded.</p>
          <button type="button" onClick={() => refetch()}>
            Retry
          </button>
        </div>
      )}

      {!isLoading && !isError && data?.items.length === 0 && (
        <p className="notifications-page__empty">No notifications</p>
      )}

      {!isLoading && !isError && data && data.items.length > 0 && (
        <div className="notifications-page__list">
          {data.items.map((notification) => (
            <NotificationItem key={notification.id} notification={notification} />
          ))}
        </div>
      )}

      {data && data.totalCount > PAGE_SIZE && (
        <div className="notifications-page__pagination">
          <button type="button" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>
            Previous
          </button>
          <span>
            Page {page} of {totalPages}
          </span>
          <button type="button" disabled={page >= totalPages} onClick={() => setPage((p) => p + 1)}>
            Next
          </button>
        </div>
      )}
    </div>
  );
}
