import 'package:flutter/foundation.dart';

import '../../services/api_client/models/inbox_notification.dart';
import '../network/api_exception.dart';
import '../widgets/async_data.dart';
import 'inbox_service.dart';

class InboxController extends ChangeNotifier {
  InboxController(this._service);

  final InboxService _service;

  int unreadCount = 0;
  AsyncData<List<InboxNotification>> items = const AsyncData.loading();
  bool unreadOnly = false;

  Future<void> refreshUnreadCount() async {
    try {
      unreadCount = await _service.unreadCount();
      notifyListeners();
    } on ApiException {
      // Best effort - the inbox screen itself will surface a real error if opened.
    }
  }

  Future<void> load({bool showLoading = true}) async {
    if (showLoading) {
      items = const AsyncData.loading();
      notifyListeners();
    }

    try {
      final page = await _service.list(unreadOnly: unreadOnly);
      items = AsyncData.ready(page.items);
    } on ApiException catch (error) {
      items = AsyncData.failed(error);
    }
    notifyListeners();
  }

  Future<void> setUnreadOnly(bool value) async {
    if (unreadOnly == value) return;
    unreadOnly = value;
    await load();
  }

  Future<void> markRead(String id) async {
    final current = items.valueOrNull;
    if (current == null) return;

    final index = current.indexWhere((n) => n.id == id);
    if (index == -1 || current[index].readAt != null) return;

    try {
      final updated = await _service.markRead(id);
      items = AsyncData.ready([
        for (final n in current) if (n.id == id) updated else n,
      ]);
      if (unreadCount > 0) unreadCount--;
      notifyListeners();
    } on ApiException {
      // Leave it unread locally; the next refresh reconciles with the server.
    }
  }

  Future<void> markAllRead() async {
    try {
      unreadCount = await _service.markAllRead();
      final current = items.valueOrNull;
      if (current != null) {
        final now = DateTime.now();
        items = AsyncData.ready([
          for (final n in current)
            InboxNotification(
              id: n.id,
              type: n.type,
              title: n.title,
              body: n.body,
              entityType: n.entityType,
              entityId: n.entityId,
              readAt: n.readAt ?? now,
              createdAt: n.createdAt,
            ),
        ]);
      }
      notifyListeners();
    } on ApiException {
      // Best effort - a refresh will show the true state.
    }
  }
}
