import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { Bell } from 'lucide-react';
import { NotificationIcon } from '../components/NotificationIcon';
import { PaginationControls } from '../components/ui/pagination-controls';
import {
  getMyUnreadNotificationCountQueryKey,
  listMyNotificationsOptions,
  listMyNotificationsQueryKey,
  markAllNotificationsReadMutation,
  markNotificationReadMutation,
} from '../services/api/generated/@tanstack/react-query.gen';
import type { InboxNotification } from '../services/api/generated';
import { localDateTime, timeAgo } from '../types/datetime';
import { routeForNotification } from '../types/notifications';

const PAGE_SIZE = 20;

export function NotificationsPage() {
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const [unreadOnly, setUnreadOnly] = useState(false);
  const [page, setPage] = useState(1);

  const notifications = useQuery({
    ...listMyNotificationsOptions({ query: { unreadOnly, page, pageSize: PAGE_SIZE } }),
  });

  function invalidateInbox() {
    queryClient.invalidateQueries({ queryKey: listMyNotificationsQueryKey() });
    queryClient.invalidateQueries({ queryKey: getMyUnreadNotificationCountQueryKey() });
  }

  const markRead = useMutation({
    ...markNotificationReadMutation(),
    onSuccess: invalidateInbox,
  });

  const markAllRead = useMutation({
    ...markAllNotificationsReadMutation(),
    onSuccess: () => {
      toast.success('Everything is marked read.');
      invalidateInbox();
    },
  });

  function openItem(notification: InboxNotification) {
    if (!notification.read_at && notification.id) markRead.mutate({ path: { id: notification.id } });
    const route = routeForNotification(notification);
    if (route) navigate(route);
  }

  const rows = notifications.data?.items ?? [];
  const totalPages = notifications.data?.total_pages ?? 1;

  return (
    <>
      <h1>Notifications</h1>
      <p className="muted">Things that concern you, across the whole hospital.</p>

      <div className="table-section">
        <div className="notifications-toolbar">
          <div className="tabs notifications-filter">
            <button
              type="button"
              aria-pressed={!unreadOnly}
              onClick={() => { setUnreadOnly(false); setPage(1); }}
            >
              All
            </button>
            <button
              type="button"
              aria-pressed={unreadOnly}
              onClick={() => { setUnreadOnly(true); setPage(1); }}
            >
              Unread
            </button>
          </div>
          <button
            type="button"
            className="secondary"
            disabled={markAllRead.isPending}
            onClick={() => markAllRead.mutate({})}
          >
            {markAllRead.isPending ? 'Marking…' : 'Mark all read'}
          </button>
        </div>

        {notifications.isPending && <p className="empty">Loading notifications…</p>}

        {notifications.isError && (
          <p className="empty">
            Notifications could not be loaded.{' '}
            <button type="button" className="secondary" onClick={() => notifications.refetch()}>
              Try again
            </button>
          </p>
        )}

        {notifications.isSuccess && rows.length === 0 && (
          <div className="notification-popover-empty">
            <Bell size={24} aria-hidden="true" />
            <p>{unreadOnly ? 'Nothing unread. You are all caught up.' : 'Nothing here yet.'}</p>
          </div>
        )}

        {rows.length > 0 && (
          <div className="notifications-card">
            <ul className="notifications-feed">
              {rows.map((notification) => (
                <li key={notification.id} className={notification.read_at ? undefined : 'notifications-feed-item--unread'}>
                  <NotificationIcon notification={notification} />
                  <div className="notifications-feed-text">
                    <button type="button" className="linklike notifications-feed-title" onClick={() => openItem(notification)}>
                      {notification.title}
                    </button>
                    {notification.body && <p className="notifications-feed-body">{notification.body}</p>}
                    {notification.created_at && (
                      <time className="notifications-feed-meta" dateTime={notification.created_at} title={localDateTime(notification.created_at)}>
                        {timeAgo(notification.created_at)}
                      </time>
                    )}
                  </div>
                  {!notification.read_at && (
                    <button
                      type="button"
                      className="linklike notifications-feed-mark"
                      disabled={markRead.isPending}
                      onClick={() => notification.id && markRead.mutate({ path: { id: notification.id } })}
                    >
                      Mark read
                    </button>
                  )}
                </li>
              ))}
            </ul>
            <div className="table-footer">
              <PaginationControls label="Notifications" page={page} totalPages={totalPages} onPageChange={setPage} />
            </div>
          </div>
        )}
      </div>
    </>
  );
}
