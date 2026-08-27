import { useState } from 'react';
import { Search } from 'lucide-react';
import {
  useBulkDismiss,
  useBulkMarkRead,
  useMarkAllNotificationsRead,
  useNotifications,
} from '@/hooks/useNotifications';
import { useProjects } from '@/hooks/useProjects';
import { NotificationItem } from '@/components/layout/NotificationItem';
import type { NotificationCategory, NotificationPriority } from '@/types/notification';
import './NotificationsPage.css';

const PAGE_SIZE = 20;

const TABS: { id: NotificationCategory; label: string }[] = [
  { id: 'all', label: 'All' },
  { id: 'unread', label: 'Unread' },
  { id: 'mentions', label: 'Mentions' },
  { id: 'tasks', label: 'Tasks' },
  { id: 'projects', label: 'Projects' },
  { id: 'files', label: 'Files' },
  { id: 'automations', label: 'Automations' },
  { id: 'system', label: 'System' },
];

export function NotificationsPage() {
  const [tab, setTab] = useState<NotificationCategory>('all');
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [priority, setPriority] = useState<NotificationPriority | ''>('');
  const [projectId, setProjectId] = useState('');
  const [selecting, setSelecting] = useState(false);
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());

  const markAllRead = useMarkAllNotificationsRead();
  const bulkMarkRead = useBulkMarkRead();
  const bulkDismiss = useBulkDismiss();
  const { data: projects } = useProjects();

  const { data, isLoading, isError, refetch } = useNotifications({
    isRead: tab === 'unread' ? false : undefined,
    category: tab === 'all' || tab === 'unread' ? undefined : tab,
    search: search.trim() || undefined,
    priority: priority || undefined,
    projectId: projectId || undefined,
    page,
    pageSize: PAGE_SIZE,
  });

  function changeTab(next: NotificationCategory) {
    setTab(next);
    setPage(1);
    setSelectedIds(new Set());
  }

  function toggleSelected(id: string) {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }

  function toggleSelectAll() {
    if (!data) return;
    setSelectedIds((prev) => (prev.size === data.items.length ? new Set() : new Set(data.items.map((n) => n.id))));
  }

  function exitSelecting() {
    setSelecting(false);
    setSelectedIds(new Set());
  }

  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / PAGE_SIZE)) : 1;

  return (
    <div className="notifications-page">
      <div className="notifications-page__header">
        <h1>Notifications</h1>
        <div className="notifications-page__header-actions">
          <button type="button" className="notifications-page__mark-all" onClick={() => setSelecting((s) => !s)}>
            {selecting ? 'Cancel' : 'Select'}
          </button>
          <button type="button" className="notifications-page__mark-all" onClick={() => markAllRead.mutate()} disabled={markAllRead.isPending}>
            Mark all read
          </button>
        </div>
      </div>

      <nav className="notifications-page__tabs" role="tablist">
        {TABS.map((option) => (
          <button
            key={option.id}
            type="button"
            role="tab"
            aria-selected={tab === option.id}
            className={`notifications-page__tab${tab === option.id ? ' notifications-page__tab--active' : ''}`}
            onClick={() => changeTab(option.id)}
          >
            {option.label}
          </button>
        ))}
      </nav>

      <div className="notifications-page__filters">
        <div className="notifications-page__search">
          <Search size={14} />
          <input
            type="text"
            placeholder="Search notifications..."
            value={search}
            onChange={(e) => {
              setSearch(e.target.value);
              setPage(1);
            }}
            aria-label="Search notifications"
          />
        </div>
        <select
          value={priority}
          onChange={(e) => {
            setPriority(e.target.value as NotificationPriority | '');
            setPage(1);
          }}
          aria-label="Filter by priority"
        >
          <option value="">Any priority</option>
          <option value="Urgent">Urgent</option>
          <option value="Important">Important</option>
          <option value="Normal">Normal</option>
        </select>
        <select
          value={projectId}
          onChange={(e) => {
            setProjectId(e.target.value);
            setPage(1);
          }}
          aria-label="Filter by project"
        >
          <option value="">Any project</option>
          {projects?.map((project) => (
            <option key={project.id} value={project.id}>
              {project.name}
            </option>
          ))}
        </select>
      </div>

      {selecting && data && data.items.length > 0 && (
        <div className="notifications-page__bulk-bar">
          <label>
            <input type="checkbox" checked={selectedIds.size === data.items.length} onChange={toggleSelectAll} />
            {selectedIds.size} selected
          </label>
          <button
            type="button"
            disabled={selectedIds.size === 0 || bulkMarkRead.isPending}
            onClick={() => bulkMarkRead.mutate(Array.from(selectedIds), { onSuccess: exitSelecting })}
          >
            Mark as Read
          </button>
          <button
            type="button"
            disabled={selectedIds.size === 0 || bulkDismiss.isPending}
            onClick={() => bulkDismiss.mutate(Array.from(selectedIds), { onSuccess: exitSelecting })}
          >
            Dismiss
          </button>
        </div>
      )}

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
            <NotificationItem
              key={notification.id}
              notification={notification}
              selectable={selecting}
              selected={selectedIds.has(notification.id)}
              onToggleSelected={toggleSelected}
            />
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
