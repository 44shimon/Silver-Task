import { useEffect } from 'react';
import { useMutation, useQuery, useQueryClient, type QueryClient } from '@tanstack/react-query';
import * as signalR from '@microsoft/signalr';
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

export function useNotification(id: string | null) {
  return useQuery({
    queryKey: ['notifications', 'detail', id],
    queryFn: () => notificationsApi.getById(id!),
    enabled: !!id,
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

export function useBulkMarkRead() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (ids: string[]) => notificationsApi.bulkMarkRead(ids),
    // Bulk selections span an arbitrary mix of already-read/unread rows — reconciling with the
    // server once it settles (same as "mark all read") is simpler and just as correct as trying
    // to compute the exact unread-count delta client-side.
    onSettled: () => queryClient.invalidateQueries({ queryKey: ['notifications'] }),
  });
}

export function useBulkDismiss() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (ids: string[]) => notificationsApi.bulkDismiss(ids),
    onSettled: () => queryClient.invalidateQueries({ queryKey: ['notifications'] }),
  });
}

/** Phase 44 — "Clear read notifications" (spec #70); the unread count is unaffected since only
 * already-read notifications are ever removed by this. */
export function useClearReadNotifications() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => notificationsApi.clearRead(),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['notifications'] }),
  });
}

/**
 * Phase 36 real-time push — connects to the server's NotificationHub (cookie-auth, same as every
 * other request) and invalidates the notification queries whenever the server pushes
 * "notificationReceived", so the bell/list update without waiting for the next 60s poll. That
 * poll (see useUnreadCount above) is deliberately left in place as the offline-resilience
 * fallback — this hook only makes the common case (tab open, connection alive) feel instant.
 * Mount once for the whole authenticated app (see AppShell) rather than per-page.
 */
export function useNotificationHub() {
  const queryClient = useQueryClient();

  useEffect(() => {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/notifications', { withCredentials: true })
      .withAutomaticReconnect()
      .build();

    connection.on('notificationReceived', () => {
      queryClient.invalidateQueries({ queryKey: ['notifications'] });
    });

    // On reconnect (e.g. after a laptop sleep/network blip), refetch immediately rather than
    // waiting for the next poll interval — this is the "fetch missed notifications on
    // reconnect" behavior the offline-resilience requirement asks for.
    connection.onreconnected(() => {
      queryClient.invalidateQueries({ queryKey: ['notifications'] });
    });

    connection.start().catch(() => {
      // Best-effort — the 60s poll already covers this browser regardless of whether the
      // real-time connection ever comes up (e.g. blocked by a restrictive proxy).
    });

    return () => {
      connection.stop();
    };
  }, [queryClient]);
}
