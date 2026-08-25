import { httpClient } from './httpClient';
import type { NotificationFilter, NotificationList } from '@/types/notification';

function buildQuery(filter: NotificationFilter): string {
  const params = new URLSearchParams();
  if (filter.isRead !== undefined) params.set('isRead', String(filter.isRead));
  if (filter.page !== undefined) params.set('page', String(filter.page));
  if (filter.pageSize !== undefined) params.set('pageSize', String(filter.pageSize));
  const query = params.toString();
  return query ? `?${query}` : '';
}

/** Every endpoint resolves the caller from the auth cookie server-side (User.GetUserId()) —
 * there is no user id parameter here to get wrong. */
export const notificationsApi = {
  list: (filter: NotificationFilter) => httpClient.get<NotificationList>(`/notifications${buildQuery(filter)}`),
  unreadCount: () => httpClient.get<{ count: number }>('/notifications/unread-count'),
  markRead: (id: string) => httpClient.put<void>(`/notifications/${id}/read`),
  markUnread: (id: string) => httpClient.put<void>(`/notifications/${id}/unread`),
  markAllRead: () => httpClient.put<void>('/notifications/read-all'),
  remove: (id: string) => httpClient.delete<void>(`/notifications/${id}`),
};
