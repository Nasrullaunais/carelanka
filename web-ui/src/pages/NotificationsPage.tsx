import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { Table } from '../components/Table';
import { PaginationControls } from '../components/ui/pagination-controls';
import {
  getMyUnreadNotificationCountQueryKey,
  listMyNotificationsOptions,
  listMyNotificationsQueryKey,
  markAllNotificationsReadMutation,
  markNotificationReadMutation,
} from '../services/api/generated/@tanstack/react-query.gen';
import type { InboxNotification } from '../services/api/generated';
import { localDateTime } from '../types/datetime';
import { notificationTypeLabels, routeForNotification } from '../types/notifications';

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
        <div className="row">
          <div className="tabs">
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
          <div className="actions">
            <button
              type="button"
              className="secondary"
              disabled={markAllRead.isPending}
              onClick={() => markAllRead.mutate({})}
            >
              {markAllRead.isPending ? 'Marking…' : 'Mark all read'}
            </button>
          </div>
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
          <p className="empty">{unreadOnly ? 'Nothing unread.' : 'Nothing here yet.'}</p>
        )}

        {rows.length > 0 && (
          <Table footer={<PaginationControls label="Notifications" page={page} totalPages={totalPages} onPageChange={setPage} />}>
            <thead>
              <tr>
                <th>Type</th>
                <th>Message</th>
                <th>When</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {rows.map((notification) => (
                <tr key={notification.id} className={notification.read_at ? undefined : 'notification-row--unread'}>
                  <td>{notification.type && notificationTypeLabels[notification.type]}</td>
                  <td>
                    <button type="button" className="link-button" onClick={() => openItem(notification)}>
                      {notification.title}
                    </button>
                    {notification.body && <div className="muted">{notification.body}</div>}
                  </td>
                  <td>{notification.created_at && localDateTime(notification.created_at)}</td>
                  <td>
                    {!notification.read_at && (
                      <button
                        type="button"
                        className="secondary"
                        disabled={markRead.isPending}
                        onClick={() => notification.id && markRead.mutate({ path: { id: notification.id } })}
                      >
                        Mark read
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </Table>
        )}
      </div>
    </>
  );
}
