import '../../services/api_client/care_lanka_api.dart';
import '../../services/api_client/models/inbox_notification.dart';
import '../../services/api_client/models/inbox_notification_paged_result.dart';
import '../network/api.dart';

abstract interface class InboxService {
  Future<InboxNotificationPagedResult> list({required bool unreadOnly});
  Future<int> unreadCount();
  Future<InboxNotification> markRead(String id);
  Future<int> markAllRead();
}

final class GeneratedInboxService implements InboxService {
  GeneratedInboxService(CareLankaApi api) : _api = api;

  final CareLankaApi _api;

  @override
  Future<InboxNotificationPagedResult> list({required bool unreadOnly}) => callApi(
        () => _api.notifications.listMyNotifications(unreadOnly: unreadOnly),
      );

  @override
  Future<int> unreadCount() async =>
      (await callApi(_api.notifications.getMyUnreadNotificationCount)).count ?? 0;

  @override
  Future<InboxNotification> markRead(String id) =>
      callApi(() => _api.notifications.markNotificationRead(id: id));

  @override
  Future<int> markAllRead() async =>
      (await callApi(_api.notifications.markAllNotificationsRead)).count ?? 0;
}
