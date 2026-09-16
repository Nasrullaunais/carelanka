import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/auth/auth_controller.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/widgets/async_data.dart';
import '../../../core/widgets/phone_width.dart';
import '../state/equipment_confirmation_controller.dart';
import 'equipment_confirmation_screen.dart';

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
      if (mounted) context.read<EquipmentConfirmationController>().loadCount();
    });
  }

  Future<void> _openConfirmation(BuildContext context) async {
    final controller = context.read<EquipmentConfirmationController>();

    await Navigator.of(context).push(
      MaterialPageRoute(
        builder: (_) => ChangeNotifierProvider.value(
          value: controller,
          child: const EquipmentConfirmationScreen(),
        ),
      ),
    );

    controller.lock();
    await controller.loadCount();
  }

  @override
  Widget build(BuildContext context) {
    final controller = context.watch<EquipmentConfirmationController>();
    final theme = Theme.of(context);

    final waiting = switch (controller.awaitingCount) {
      AsyncReady(:final value) when value == 0 => 'Nothing is waiting for confirmation.',
      AsyncReady(:final value) when value == 1 => '1 new item is waiting for confirmation.',
      AsyncReady(:final value) => '$value new items are waiting for confirmation.',
      AsyncFailed(:final error) => error.message,
      AsyncLoading() => 'Checking for new items…',
    };

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
          onRefresh: controller.loadCount,
          child: ListView(
            padding: const EdgeInsets.all(AppTheme.gutter),
            children: [
              Card(
                clipBehavior: Clip.antiAlias,
                child: InkWell(
                  onTap: () => _openConfirmation(context),
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
                          child: Icon(
                            Icons.fact_check_outlined,
                            color: theme.colorScheme.onPrimaryContainer,
                          ),
                        ),
                        const SizedBox(width: 16),
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text('Equipment confirmation', style: theme.textTheme.titleMedium),
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
              ),
            ],
          ),
        ),
      ),
    );
  }
}
