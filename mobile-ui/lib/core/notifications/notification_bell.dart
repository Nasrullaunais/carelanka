import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import 'inbox_controller.dart';
import 'inbox_screen.dart';

class NotificationBell extends StatelessWidget {
  const NotificationBell({super.key});

  @override
  Widget build(BuildContext context) {
    final unreadCount = context.select<InboxController, int>((c) => c.unreadCount);

    return IconButton(
      tooltip: 'Notifications',
      onPressed: () => context.push(InboxPaths.home),
      icon: Badge(
        isLabelVisible: unreadCount > 0,
        label: Text(unreadCount > 99 ? '99+' : '$unreadCount'),
        child: const Icon(Icons.notifications_outlined),
      ),
    );
  }
}
