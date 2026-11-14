import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { notificationKeys } from '@/features/workspaces/query-keys';
import type { ApiError } from '@/shared/api/errors';
import { request } from '@/shared/api/request';
import type { NotificationPage, UnreadCountDto } from '@/shared/api/types';

const POLL_MS = 30_000;

/** Unread badge. Polled: notifications are not part of the board realtime channel. */
export function useUnreadCount() {
  return useQuery({
    queryKey: notificationKeys.unreadCount(),
    queryFn: () => request<UnreadCountDto>({ method: 'GET', url: '/notifications/unread-count' }),
    select: (data) => data.count,
    refetchInterval: POLL_MS,
  });
}

export function useNotifications(enabled: boolean) {
  return useQuery({
    queryKey: notificationKeys.list(),
    queryFn: () =>
      request<NotificationPage>({
        method: 'GET',
        url: '/notifications',
        params: { page: 1, pageSize: 20 },
      }),
    enabled,
  });
}

export function useMarkNotificationRead() {
  const queryClient = useQueryClient();
  return useMutation<void, ApiError, string>({
    mutationFn: (id) => request<void>({ method: 'POST', url: `/notifications/${id}/read` }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: notificationKeys.all }),
  });
}

export function useMarkAllNotificationsRead() {
  const queryClient = useQueryClient();
  return useMutation<void, ApiError, void>({
    mutationFn: () => request<void>({ method: 'POST', url: '/notifications/read-all' }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: notificationKeys.all }),
  });
}
