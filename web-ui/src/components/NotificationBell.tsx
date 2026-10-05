import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Button, Popover, PopoverContent, PopoverDialog } from '@heroui/react';
import { Bell, CheckCheck } from 'lucide-react';
import {
  getMyUnreadNotificationCountOptions,
  getMyUnreadNotificationCountQueryKey,
  listMyNotificationsOptions,
  listMyNotificationsQueryKey,
  markAllNotificationsReadMutation,
  markNotificationReadMutation,
} from '../services/api/generated/@tanstack/react-query.gen';
import type { InboxNotification } from '../services/api/generated';
import { localDateTime, timeAgo } from '../types/datetime';
import { routeForNotification } from '../types/notifications';
import { NotificationIcon } from './NotificationIcon';

const PREVIEW_SIZE = 8;

export function NotificationBell() {
  const [open, setOpen] = useState(false);
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const unreadCount = useQuery({
    ...getMyUnreadNotificationCountOptions(),
    refetchInterval: 60_000,
  });
  const inbox = useQuery({
    ...listMyNotificationsOptions({ query: { page: 1, pageSize: PREVIEW_SIZE } }),
    enabled: open,
  });

  function invalidateInbox() {
    queryClient.invalidateQueries({ queryKey: getMyUnreadNotificationCountQueryKey() });
    queryClient.invalidateQueries({ queryKey: listMyNotificationsQueryKey() });
  }

  const markRead = useMutation({ ...markNotificationReadMutation(), onSuccess: invalidateInbox });
  const markAllRead = useMutation({ ...markAllNotificationsReadMutation(), onSuccess: invalidateInbox });

  const count = unreadCount.data?.count ?? 0;
  const items = inbox.data?.items ?? [];

  function openItem(notification: InboxNotification) {
    if (!notification.read_at && notification.id) markRead.mutate({ path: { id: notification.id } });
    setOpen(false);
    const route = routeForNotification(notification);
    if (route) navigate(route);
  }

  return (
    <Popover isOpen={open} onOpenChange={setOpen}>
      <Button variant="ghost" className="notification-bell" aria-label={count > 0 ? `Notifications, ${count} unread` : 'Notifications'}>
        <Bell size={19} aria-hidden="true" />
        {count > 0 && <span className="notification-bell-badge">{count > 99 ? '99+' : count}</span>}
      </Button>
      <PopoverContent placement="bottom end" className="notification-popover">
        <PopoverDialog aria-label="Recent notifications">
          <div className="notification-popover-header">
            <p className="notification-popover-title">
              Notifications
              {count > 0 && <span className="muted"> · {count} unread</span>}
            </p>
            {count > 0 && (
              <button
                type="button"
                className="linklike notification-popover-mark-all"
                disabled={markAllRead.isPending}
                onClick={() => markAllRead.mutate({})}
              >
                <CheckCheck size={15} aria-hidden="true" />
                Mark all read
              </button>
            )}
          </div>
          {inbox.isPending && <p className="empty">Loading…</p>}
          {inbox.isError && (
            <p className="empty">
              Notifications could not be loaded.{' '}
              <button type="button" className="secondary" onClick={() => inbox.refetch()}>Try again</button>
            </p>
          )}
          {inbox.isSuccess && items.length === 0 && (
            <div className="notification-popover-empty">
              <Bell size={22} aria-hidden="true" />
              <p>You're all caught up.</p>
            </div>
          )}
          {items.length > 0 && (
            <ul className="notification-list">
              {items.map((notification) => (
                <li key={notification.id}>
                  <button
                    type="button"
                    className={`notification-item${notification.read_at ? '' : ' notification-item--unread'}`}
                    onClick={() => openItem(notification)}
                  >
                    <NotificationIcon notification={notification} />
                    <span className="notification-item-text">
                      <span className="notification-item-title">{notification.title}</span>
                      {notification.body && <span className="notification-item-body">{notification.body}</span>}
                      {notification.created_at && (
                        <time
                          className="notification-item-time"
                          dateTime={notification.created_at}
                          title={localDateTime(notification.created_at)}
                        >
                          {timeAgo(notification.created_at)}
                        </time>
                      )}
                    </span>
                    {!notification.read_at && <span className="notification-item-dot" aria-label="Unread" />}
                  </button>
                </li>
              ))}
            </ul>
          )}
          <Button
            variant="ghost"
            className="notification-popover-all"
            onPress={() => {
              setOpen(false);
              navigate('/notifications');
            }}
          >
            View all notifications
          </Button>
        </PopoverDialog>
      </PopoverContent>
    </Popover>
  );
}
