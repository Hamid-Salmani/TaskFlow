import { AtSign, Bell, UserPlus } from 'lucide-react';
import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import type { NotificationDto } from '@/shared/api/types';
import { cn } from '@/shared/lib/cn';
import { formatRelativeTime } from '@/shared/lib/format';
import { Button, Dropdown, type DropdownItem } from '@/shared/ui';
import {
  useMarkAllNotificationsRead,
  useMarkNotificationRead,
  useNotifications,
  useUnreadCount,
} from '../hooks/useNotifications';

function NotificationRow({ notification }: { notification: NotificationDto }) {
  const Icon = notification.type === 'Mentioned' ? AtSign : UserPlus;
  return (
    <span className="flex w-72 items-start gap-2 text-left">
      <Icon className="mt-0.5 size-4 shrink-0 text-muted" aria-hidden="true" />
      <span className="min-w-0 flex-1">
        <span
          className={cn('block whitespace-normal text-sm', !notification.isRead && 'font-semibold')}
        >
          {notification.message}
        </span>
        <span className="block text-xs text-muted">
          {formatRelativeTime(notification.createdAt, new Date())}
        </span>
      </span>
      {notification.isRead ? null : (
        <>
          <span className="mt-1.5 size-2 shrink-0 rounded-full bg-primary" aria-hidden="true" />
          <span className="sr-only">Unread</span>
        </>
      )}
    </span>
  );
}

/** Bell with an unread badge. Selecting a notification marks it read and opens its card. */
export function NotificationsMenu() {
  const navigate = useNavigate();
  const [open, setOpen] = useState(false);
  const unread = useUnreadCount().data ?? 0;
  const notifications = useNotifications(open);
  const markRead = useMarkNotificationRead();
  const markAllRead = useMarkAllNotificationsRead();

  const items: DropdownItem[] = (notifications.data?.items ?? []).map((notification) => ({
    id: notification.id,
    label: <NotificationRow notification={notification} />,
    onSelect: () => {
      if (!notification.isRead) {
        markRead.mutate(notification.id);
      }
      navigate(
        notification.cardId === null || notification.cardId === undefined
          ? `/boards/${notification.boardId}`
          : `/boards/${notification.boardId}?card=${notification.cardId}`,
      );
    },
  }));

  return (
    <Dropdown
      triggerLabel={unread > 0 ? `Notifications, ${unread} unread` : 'Notifications'}
      trigger={
        <span className="relative">
          <Bell className="size-5" aria-hidden="true" />
          {unread > 0 ? (
            <span
              aria-hidden="true"
              className="absolute -right-2 -top-2 flex min-w-4 items-center justify-center rounded-full bg-danger px-1 text-[10px] font-bold leading-4 text-danger-foreground"
            >
              {unread > 99 ? '99+' : unread}
            </span>
          ) : null}
        </span>
      }
      triggerClassName="size-9 justify-center"
      align="end"
      onOpenChange={setOpen}
      header={
        <div className="flex items-center justify-between gap-4 px-3 py-2">
          <span className="text-sm font-semibold">Notifications</span>
          <Button
            size="sm"
            variant="ghost"
            disabled={unread === 0 || markAllRead.isPending}
            onClick={() => markAllRead.mutate()}
          >
            Mark all read
          </Button>
        </div>
      }
      emptyMessage={
        notifications.isPending
          ? 'Loading…'
          : notifications.isError
            ? 'Could not load notifications.'
            : 'You are all caught up.'
      }
      items={items}
    />
  );
}
