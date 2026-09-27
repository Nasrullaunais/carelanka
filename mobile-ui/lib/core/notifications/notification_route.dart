import '../../features/emergency/emergency_routes.dart';
import '../../services/api_client/models/inbox_notification.dart';
import '../../services/api_client/models/notification_type.dart';

/// One entry per [NotificationType] so a new type with no route is a compile error.
/// The recipient is the assigned crew member, same as an opened push (app.dart's `_openMyRun`).
const Map<NotificationType, String Function(InboxNotification)> _routes = {
  NotificationType.dispatchAssigned: _myRun,
};

String _myRun(InboxNotification notification) => EmergencyPaths.myRun;

String? routeForNotification(InboxNotification notification) {
  final type = notification.type;
  if (type == null || type == NotificationType.$unknown) return null;
  return _routes[type]?.call(notification);
}

/// Same map, entered from a push payload (`type`/`entity_type`/`entity_id`) instead of an
/// inbox row - a background or cold-start tap never carries the full [InboxNotification].
String? routeForPushData(Map<String, String> data) {
  final typeWire = data['type'];
  if (typeWire == null) return null;

  return routeForNotification(InboxNotification(
    type: NotificationType.fromJson(typeWire),
    entityType: data['entity_type'],
    entityId: data['entity_id'],
  ));
}
