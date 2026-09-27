import type { InboxNotification, NotificationType } from '../services/api/generated';

export const notificationTypeLabels: Record<NotificationType, string> = {
  dispatch_assigned: 'Dispatch assigned',
};

// One route per NotificationType so a new type with no route is a compile error, not a dead click.
const notificationRoutes: Record<NotificationType, (entityId: string) => string> = {
  dispatch_assigned: () => '/emergency',
};

export function routeForNotification(notification: InboxNotification): string | null {
  if (!notification.type) return null;
  return notificationRoutes[notification.type](notification.entity_id ?? '');
}
