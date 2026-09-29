import 'package:carelanka_mobile/core/notifications/inbox_controller.dart';
import 'package:carelanka_mobile/core/notifications/inbox_service.dart';
import 'package:carelanka_mobile/services/api_client/models/inbox_notification.dart';
import 'package:carelanka_mobile/services/api_client/models/inbox_notification_paged_result.dart';
import 'package:provider/provider.dart';

/// An always-empty inbox, for screens under test that only need the bell to render.
final class FakeInboxService implements InboxService {
  @override
  Future<InboxNotificationPagedResult> list({required bool unreadOnly}) async =>
      const InboxNotificationPagedResult(items: [], page: 1, pageSize: 20, totalItems: 0, totalPages: 0);

  @override
  Future<int> unreadCount() async => 0;

  @override
  Future<InboxNotification> markRead(String id) async => InboxNotification(id: id);

  @override
  Future<int> markAllRead() async => 0;
}

ChangeNotifierProvider<InboxController> fakeInboxProvider() =>
    ChangeNotifierProvider<InboxController>(create: (_) => InboxController(FakeInboxService()));
