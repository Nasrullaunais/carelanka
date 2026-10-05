import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { X } from 'lucide-react';
import {
  getMyUnreadNotificationCountQueryKey,
  listMyNotificationsQueryKey,
  markNotificationReadMutation,
} from '../services/api/generated/@tanstack/react-query.gen';
import type { InboxNotification } from '../services/api/generated';
import { useSession } from '../services/auth/useSession';
import { onNewNotifications } from '../services/realtime/notifications';
import { canManageEmergency } from '../types/permissions';
import { isUrgentNotification, notificationTypeLabels, routeForNotification } from '../types/notifications';
import { CallNotificationDetails } from '../features/emergency/components/call-notification-details';
import { NotificationIcon } from './NotificationIcon';

const MAX_VISIBLE = 3;
const QUIET_POPUP_MS = 8_000;

export function NotificationPopups() {
  const [popups, setPopups] = useState<InboxNotification[]>([]);
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  useEffect(() => onNewNotifications((fresh) => {
    setPopups((current) => [...fresh, ...current.filter((popup) => !fresh.some((item) => item.id === popup.id))]);
  }), []);

  const markRead = useMutation({
    ...markNotificationReadMutation(),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: getMyUnreadNotificationCountQueryKey() });
      queryClient.invalidateQueries({ queryKey: listMyNotificationsQueryKey() });
    },
  });

  const dismiss = useCallback((id?: string) => {
    setPopups((current) => current.filter((popup) => popup.id !== id));
  }, []);

  function open(notification: InboxNotification) {
    dismiss(notification.id);
    if (notification.id) markRead.mutate({ path: { id: notification.id } });
    const route = routeForNotification(notification);
    if (route) navigate(route);
  }

  if (popups.length === 0) return null;

  const hidden = popups.length - MAX_VISIBLE;
  return (
    <section className="notification-popups" aria-label="New notifications">
      {popups.slice(0, MAX_VISIBLE).map((notification) => (
        <NotificationPopup
          key={notification.id}
          notification={notification}
          onOpen={open}
          onDismiss={dismiss}
        />
      ))}
      {hidden > 0 && (
        <div className="notification-popups-more">
          <span>{hidden} more new {hidden === 1 ? 'notification' : 'notifications'}</span>
          <button type="button" className="linklike" onClick={() => { setPopups([]); navigate('/notifications'); }}>
            See all
          </button>
          <button type="button" className="linklike" onClick={() => setPopups([])}>
            Dismiss all
          </button>
        </div>
      )}
    </section>
  );
}

function NotificationPopup({ notification, onOpen, onDismiss }: {
  notification: InboxNotification;
  onOpen: (notification: InboxNotification) => void;
  onDismiss: (id?: string) => void;
}) {
  const session = useSession();
  const urgent = isUrgentNotification(notification);
  const [paused, setPaused] = useState(false);

  useEffect(() => {
    if (urgent || paused) return;
    const timer = window.setTimeout(() => onDismiss(notification.id), QUIET_POPUP_MS);
    return () => window.clearTimeout(timer);
  }, [urgent, paused, onDismiss, notification.id]);

  const showsCall = notification.entity_type === 'emergency_call'
    && notification.entity_id
    && canManageEmergency(session?.principal.role);

  return (
    <article
      className={`notification-popup${urgent ? ' notification-popup--urgent' : ''}`}
      role={urgent ? 'alert' : 'status'}
      aria-label={notification.title ?? 'New notification'}
      onMouseEnter={() => setPaused(true)}
      onMouseLeave={() => setPaused(false)}
      onFocus={() => setPaused(true)}
    >
      <NotificationIcon notification={notification} />
      <div className="notification-popup-content">
        <p className="notification-popup-title">
          {notification.title ?? (notification.type ? notificationTypeLabels[notification.type] : 'New notification')}
          {urgent && <span className="notification-popup-urgent">Urgent</span>}
        </p>
        {showsCall
          ? <CallNotificationDetails callId={notification.entity_id!} fallback={notification.body} />
          : notification.body && <p className="notification-popup-body">{notification.body}</p>}
        <div className="notification-popup-actions">
          <button type="button" onClick={() => onOpen(notification)}>{openLabel(notification)}</button>
          <button type="button" className="secondary" onClick={() => onDismiss(notification.id)}>Later</button>
        </div>
      </div>
      <button type="button" className="notification-popup-close" aria-label="Dismiss notification" onClick={() => onDismiss(notification.id)}>
        <X size={16} aria-hidden="true" />
      </button>
    </article>
  );
}

function openLabel(notification: InboxNotification): string {
  const route = routeForNotification(notification);
  if (route?.startsWith('/emergency/calls/')) return 'Open call';
  if (route === '/emergency/cancellations') return 'Review request';
  return 'Open';
}
