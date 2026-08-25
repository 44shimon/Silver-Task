import { useMutation, useQuery, useQueryClient, type QueryClient } from '@tanstack/react-query';
import { notificationsApi } from '@/api/notificationsApi';
import type { AppNotification, NotificationFilter, NotificationList } from '@/types/notification';

const LIST_ROOT_KEY = ['notifications', 'list'] as const;
const notificationsListKey = (filter: NotificationFilter) => [...LIST_ROOT_KEY, filter] as const;
const unreadCountKey = ['notifications', 'unread-count'] as const;

export function useNotifications(filter: NotificationFilter = {}) {
  return useQuery({
    queryKey: notificationsListKey(filter),
    queryFn: () => notificationsApi.list(filter),
  });
}

export function useUnreadCount() {
  return useQuery({
    queryKey: unreadCountKey,
    queryFn: notificationsApi.unreadCount,
    // Polled, not pushed — there's no websocket/SSE infrastructure in this app, so a short
    // interval is the practical way to keep the header badge reasonably fresh without a full
    // page reload. Mutations below still update this cache immediately, so the common case
    // (you mark something read yourself) never waits on the poll at all.
    refetchInterval: 60_000,
  });
}

function patchCachedNotification(queryClient: QueryClient, id: string, patch: Partial<AppNotification>) {
  queryClient.setQueriesData<NotificationList>({ queryKey: LIST_ROOT_KEY }, (old) =>
    old ? { ...old, items: old.items.map((n) => (n.id === id ? { ...n, ...patch } : n)) } : old,
  );
}

function adjustUnreadCount(queryClient: QueryClient, delta: number) {
  queryClient.setQueryData<{ count: number }>(unreadCountKey, (old) =>
    old ? { count: Math.max(0, old.count + delta) } : old,
  );
}

function snapshot(queryClient: QueryClient) {
  return {
    previousLists: queryClient.getQueriesData<NotificationList>({ queryKey: LIST_ROOT_KEY }),
    previousCount: queryClient.getQueryData<{ count: number }>(unreadCountKey),
  };
}

function restore(queryClient: QueryClient, context: ReturnType<typeof snapshot> | undefined) {
  context?.previousLists.forEach(([key, data]) => queryClient.setQueryData(key, data));
  if (context?.previousCount) {
    queryClient.setQueryData(unreadCountKey, context.previousCount);
  }
}

export function useMarkNotificationRead() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => notificationsApi.markRead(id),
    onMutate: async (id) => {
      await queryClient.cancelQueries({ queryKey: ['notifications'] });
      const context = snapshot(queryClient);
      patchCachedNotification(queryClient, id, { isRead: true, readAt: new Date().toISOString() });
      adjustUnreadCount(queryClient, -1);
      return context;
    },
    onError: (_error, _id, context) => restore(queryClient, context),
  });
}

export function useMarkNotificationUnread() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => notificationsApi.markUnread(id),
    onMutate: async (id) => {
      await queryClient.cancelQueries({ queryKey: ['notifications'] });
      const context = snapshot(queryClient);
      patchCachedNotification(queryClient, id, { isRead: false, readAt: null });
      adjustUnreadCount(queryClient, 1);
      return context;
    },
    onError: (_error, _id, context) => restore(queryClient, context),
  });
}

export function useMarkAllNotificationsRead() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => notificationsApi.markAllRead(),
    onMutate: async () => {
      await queryClient.cancelQueries({ queryKey: ['notifications'] });
      const context = snapshot(queryClient);
      const now = new Date().toISOString();
      queryClient.setQueriesData<NotificationList>({ queryKey: LIST_ROOT_KEY }, (old) =>
        old ? { ...old, items: old.items.map((n) => ({ ...n, isRead: true, readAt: n.readAt ?? now })) } : old,
      );
      queryClient.setQueryData(unreadCountKey, { count: 0 });
      return context;
    },
    onError: (_error, _variables, context) => restore(queryClient, context),
    // A bulk update is harder to apply surgically across every cached page than a single-item
    // toggle — reconcile with the server once it settles rather than trusting the optimistic
    // patch to be exactly right for every cached filter/page combination.
    onSettled: () => {
      queryClient.invalidateQueries({ queryKey: ['notifications'] });
    },
  });
}

export function useDeleteNotification() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id }: { id: string; wasUnread: boolean }) => notificationsApi.remove(id),
    onMutate: async ({ id, wasUnread }) => {
      await queryClient.cancelQueries({ queryKey: ['notifications'] });
      const context = snapshot(queryClient);
      queryClient.setQueriesData<NotificationList>({ queryKey: LIST_ROOT_KEY }, (old) =>
        old
          ? { ...old, items: old.items.filter((n) => n.id !== id), totalCount: Math.max(0, old.totalCount - 1) }
          : old,
      );
      if (wasUnread) {
        adjustUnreadCount(queryClient, -1);
      }
      return context;
    },
    onError: (_error, _variables, context) => restore(queryClient, context),
  });
}
