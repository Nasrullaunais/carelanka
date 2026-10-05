import { Ambulance, Bell, Siren, TriangleAlert } from 'lucide-react';
import type { InboxNotification } from '../services/api/generated';
import { isUrgentNotification } from '../types/notifications';

export function NotificationIcon({ notification }: { notification: InboxNotification }) {
  const urgent = isUrgentNotification(notification);
  const Icon = notification.entity_type === 'emergency_call'
    ? Siren
    : notification.type?.startsWith('dispatch_') || notification.type?.startsWith('ambulance_')
      ? Ambulance
      : urgent ? TriangleAlert : Bell;

  return (
    <span className={`notification-icon${urgent ? ' notification-icon--urgent' : ''}`} aria-hidden="true">
      <Icon size={17} />
    </span>
  );
}
