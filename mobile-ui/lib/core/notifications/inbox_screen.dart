import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../services/api_client/models/inbox_notification.dart';
import '../theme/app_theme.dart';
import '../utils/friendly_date.dart';
import '../widgets/async_view.dart';
import 'inbox_controller.dart';
import 'notification_route.dart';

class InboxPaths {
  const InboxPaths._();

  static const home = '/notifications';
}

class InboxScreen extends StatefulWidget {
  const InboxScreen({super.key});

  @override
  State<InboxScreen> createState() => _InboxScreenState();
}

class _InboxScreenState extends State<InboxScreen> {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) context.read<InboxController>().load();
    });
  }

  @override
  Widget build(BuildContext context) {
    final controller = context.watch<InboxController>();

    return Scaffold(
      appBar: AppBar(
        title: const Text('Notifications'),
        actions: [
          if (controller.unreadCount > 0)
            TextButton(
              onPressed: controller.markAllRead,
              child: const Text('Mark all read'),
            ),
        ],
        bottom: PreferredSize(
          preferredSize: const Size.fromHeight(48),
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: AppTheme.gutter, vertical: 8),
            child: SegmentedButton<bool>(
              segments: const [
                ButtonSegment(value: false, label: Text('All')),
                ButtonSegment(value: true, label: Text('Unread')),
              ],
              selected: {controller.unreadOnly},
              onSelectionChanged: (selection) => controller.setUnreadOnly(selection.first),
            ),
          ),
        ),
      ),
      body: RefreshIndicator(
        onRefresh: () => Future.wait([
          controller.load(showLoading: false),
          controller.refreshUnreadCount(),
        ]),
        child: AsyncView<List<InboxNotification>>(
          state: controller.items,
          onRetry: controller.load,
          builder: (context, notifications) {
            if (notifications.isEmpty) {
              return LayoutBuilder(
                builder: (context, constraints) => SingleChildScrollView(
                  physics: const AlwaysScrollableScrollPhysics(),
                  child: ConstrainedBox(
                    constraints: BoxConstraints(minHeight: constraints.maxHeight),
                    child: const EmptyView(
                      icon: Icons.notifications_none_rounded,
                      title: 'Nothing here yet',
                      message: "You'll see updates about your care and shifts here.",
                    ),
                  ),
                ),
              );
            }

            return ListView.separated(
              physics: const AlwaysScrollableScrollPhysics(),
              itemCount: notifications.length,
              separatorBuilder: (_, __) => const Divider(height: 1),
              itemBuilder: (context, index) =>
                  _NotificationTile(notification: notifications[index]),
            );
          },
        ),
      ),
    );
  }
}

class _NotificationTile extends StatelessWidget {
  const _NotificationTile({required this.notification});

  final InboxNotification notification;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final unread = notification.readAt == null;

    return ListTile(
      tileColor: unread ? theme.colorScheme.primaryContainer.withValues(alpha: 0.25) : null,
      leading: Icon(
        unread ? Icons.circle_notifications : Icons.notifications_none_rounded,
        color: unread ? theme.colorScheme.primary : theme.colorScheme.onSurfaceVariant,
      ),
      title: Text(
        notification.title ?? '',
        style: unread ? theme.textTheme.titleSmall?.copyWith(fontWeight: FontWeight.w700) : theme.textTheme.titleSmall,
      ),
      subtitle: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(notification.body ?? ''),
          if (notification.createdAt != null) ...[
            const SizedBox(height: 4),
            Text(
              FriendlyDate.relativeDayAndTime(notification.createdAt!),
              style: theme.textTheme.bodySmall
                  ?.copyWith(color: theme.colorScheme.onSurfaceVariant),
            ),
          ],
        ],
      ),
      onTap: () => _open(context),
    );
  }

  void _open(BuildContext context) {
    final id = notification.id;
    if (id != null) context.read<InboxController>().markRead(id);

    final route = routeForNotification(notification);
    if (route != null) context.push(route);
  }
}
