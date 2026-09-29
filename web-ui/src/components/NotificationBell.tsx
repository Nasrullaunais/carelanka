import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Button, Popover, PopoverContent, PopoverDialog } from '@heroui/react';
import { Bell } from 'lucide-react';
import {
  getMyUnreadNotificationCountOptions,
  getMyUnreadNotificationCountQueryKey,
  listMyNotificationsOptions,
  listMyNotificationsQueryKey,
  markNotificationReadMutation,
} from '../services/api/generated/@tanstack/react-query.gen';
import type { InboxNotification } from '../services/api/generated';
import { localDateTime } from '../types/datetime';
import { routeForNotification } from '../types/notifications';

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

  const markRead = useMutation({
    ...markNotificationReadMutation(),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: getMyUnreadNotificationCountQueryKey() });
      queryClient.invalidateQueries({ queryKey: listMyNotificationsQueryKey() });
    },
  });

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
          <p className="notification-popover-title">Notifications</p>
          {inbox.isPending && <p className="empty">Loading…</p>}
          {inbox.isSuccess && items.length === 0 && <p className="empty">Nothing yet.</p>}
          {items.length > 0 && (
            <ul className="notification-list">
              {items.map((notification) => (
                <li key={notification.id}>
                  <button
                    type="button"
                    className={`notification-item${notification.read_at ? '' : ' notification-item--unread'}`}
                    onClick={() => openItem(notification)}
                  >
                    <span className="notification-item-title">{notification.title}</span>
                    <span className="muted notification-item-time">
                      {notification.created_at && localDateTime(notification.created_at)}
                    </span>
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
            View all
          </Button>
        </PopoverDialog>
      </PopoverContent>
    </Popover>
  );
}
