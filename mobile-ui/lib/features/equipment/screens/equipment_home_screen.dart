import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/auth/auth_controller.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/widgets/async_data.dart';
import '../../../core/widgets/phone_width.dart';
import '../state/confirmation_queue_controller.dart';
import '../state/equipment_confirmation_controller.dart';
import '../state/maintenance_confirmation_controller.dart';
import 'equipment_confirmation_screen.dart';
import 'maintenance_confirmation_screen.dart';

class EquipmentHomeScreen extends StatefulWidget {
  const EquipmentHomeScreen({super.key});

  @override
  State<EquipmentHomeScreen> createState() => _EquipmentHomeScreenState();
}

class _EquipmentHomeScreenState extends State<EquipmentHomeScreen> {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) _refresh();
    });
  }

  Future<void> _refresh() => Future.wait([
        context.read<EquipmentConfirmationController>().loadCount(),
        context.read<MaintenanceConfirmationController>().loadCount(),
      ]);

  Future<void> _open<C extends ConfirmationQueueController<Object?>>(
    BuildContext context,
    Widget screen,
  ) async {
    final controller = context.read<C>();

    await Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => ChangeNotifierProvider<C>.value(value: controller, child: screen),
      ),
    );

    controller.lock();
    await controller.loadCount();
  }

  @override
  Widget build(BuildContext context) {
    final items = context.watch<EquipmentConfirmationController>();
    final maintenance = context.watch<MaintenanceConfirmationController>();

    return PhoneWidth(
      child: Scaffold(
        appBar: AppBar(
          title: const Text('Equipment'),
          actions: [
            IconButton(
              icon: const Icon(Icons.logout),
              tooltip: 'Sign out',
              onPressed: () => context.read<AuthController>().signOut(),
            ),
          ],
        ),
        body: RefreshIndicator(
          onRefresh: _refresh,
          child: ListView(
            padding: const EdgeInsets.all(AppTheme.gutter),
            children: [
              _QueueCard(
                icon: Icons.fact_check_outlined,
                title: 'Equipment confirmation',
                waiting: _waiting(items.awaitingCount, 'new item'),
                onTap: () => _open<EquipmentConfirmationController>(
                  context,
                  const EquipmentConfirmationScreen(),
                ),
              ),
              const SizedBox(height: 10),
              _QueueCard(
                icon: Icons.build_circle_outlined,
                title: 'Maintenance confirmation',
                waiting: _waiting(maintenance.awaitingCount, 'open maintenance job'),
                onTap: () => _open<MaintenanceConfirmationController>(
                  context,
                  const MaintenanceConfirmationScreen(),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  static String _waiting(AsyncData<int> count, String noun) => switch (count) {
        AsyncReady(:final value) when value == 0 => 'Nothing is waiting for confirmation.',
        AsyncReady(:final value) when value == 1 => '1 $noun is waiting for confirmation.',
        AsyncReady(:final value) => '$value ${noun}s are waiting for confirmation.',
        AsyncFailed(:final error) => error.message,
        AsyncLoading() => 'Checking…',
      };
}

class _QueueCard extends StatelessWidget {
  const _QueueCard({
    required this.icon,
    required this.title,
    required this.waiting,
    required this.onTap,
  });

  final IconData icon;
  final String title;
  final String waiting;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Card(
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Row(
            children: [
              Container(
                padding: const EdgeInsets.all(12),
                decoration: BoxDecoration(
                  color: theme.colorScheme.primaryContainer,
                  borderRadius: BorderRadius.circular(AppTheme.radiusM),
                ),
                child: Icon(icon, color: theme.colorScheme.onPrimaryContainer),
              ),
              const SizedBox(width: 16),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(title, style: theme.textTheme.titleMedium),
                    const SizedBox(height: 4),
                    Text(
                      waiting,
                      style: theme.textTheme.bodyMedium?.copyWith(
                        color: theme.colorScheme.onSurfaceVariant,
                      ),
                    ),
                  ],
                ),
              ),
              const Icon(Icons.chevron_right),
            ],
          ),
        ),
      ),
    );
  }
}
