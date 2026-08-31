import { httpClient } from './httpClient';
import type { AppNotification, NotificationFilter, NotificationList } from '@/types/notification';

function buildQuery(filter: NotificationFilter): string {
  const params = new URLSearchParams();
  if (filter.isRead !== undefined) params.set('isRead', String(filter.isRead));
  if (filter.page !== undefined) params.set('page', String(filter.page));
  if (filter.pageSize !== undefined) params.set('pageSize', String(filter.pageSize));
  if (filter.search) params.set('search', filter.search);
  if (filter.type) params.set('type', filter.type);
  if (filter.category) params.set('category', filter.category);
  if (filter.priority) params.set('priority', filter.priority);
  if (filter.projectId) params.set('projectId', filter.projectId);
  if (filter.taskId) params.set('taskId', filter.taskId);
  if (filter.dateFrom) params.set('dateFrom', filter.dateFrom);
  if (filter.dateTo) params.set('dateTo', filter.dateTo);
  const query = params.toString();
  return query ? `?${query}` : '';
}

/** Every endpoint resolves the caller from the auth cookie server-side (User.GetUserId()) —
 * there is no user id parameter here to get wrong. */
export const notificationsApi = {
  list: (filter: NotificationFilter) => httpClient.get<NotificationList>(`/notifications${buildQuery(filter)}`),
  getById: (id: string) => httpClient.get<AppNotification>(`/notifications/${id}`),
  unreadCount: () => httpClient.get<{ count: number }>('/notifications/unread-count'),
  markRead: (id: string) => httpClient.put<void>(`/notifications/${id}/read`),
  markUnread: (id: string) => httpClient.put<void>(`/notifications/${id}/unread`),
  markAllRead: () => httpClient.put<void>('/notifications/read-all'),
  bulkMarkRead: (ids: string[]) => httpClient.post<void>('/notifications/bulk/read', { ids }),
  bulkDismiss: (ids: string[]) => httpClient.post<void>('/notifications/bulk/dismiss', { ids }),
  remove: (id: string) => httpClient.delete<void>(`/notifications/${id}`),
  /** Phase 44 — "Clear read notifications"; never removes an unread one. */
  clearRead: () => httpClient.delete<void>('/notifications/read'),
};
