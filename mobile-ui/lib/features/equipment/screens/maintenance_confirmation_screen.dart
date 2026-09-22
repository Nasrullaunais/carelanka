import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/theme/app_theme.dart';
import '../../../core/utils/friendly_date.dart';
import '../../../core/widgets/async_view.dart';
import '../../../core/widgets/phone_width.dart';
import '../../../services/api_client/models/maintenance_schedule.dart';
import '../../../services/api_client/models/maintenance_status.dart';
import '../../../services/api_client/models/maintenance_type.dart';
import '../state/maintenance_confirmation_controller.dart';
import '../widgets/confirmation_code_gate.dart';

class MaintenanceConfirmationScreen extends StatelessWidget {
  const MaintenanceConfirmationScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final controller = context.watch<MaintenanceConfirmationController>();

    return PhoneWidth(
      child: Scaffold(
        appBar: AppBar(
          title: const Text('Maintenance confirmation'),
          actions: [
            if (controller.isUnlocked)
              IconButton(
                icon: const Icon(Icons.lock_outline),
                tooltip: 'Lock',
                onPressed: controller.lock,
              ),
          ],
        ),
        body: controller.isUnlocked
            ? const _AwaitingJobs()
            : ConfirmationCodeGate(
                controller: controller,
                explanation: 'An item only goes back into service after you confirm its '
                    'maintenance is done.',
              ),
      ),
    );
  }
}

class _AwaitingJobs extends StatelessWidget {
  const _AwaitingJobs();

  @override
  Widget build(BuildContext context) {
    final controller = context.watch<MaintenanceConfirmationController>();

    return AsyncView<List<MaintenanceSchedule>>(
      state: controller.items,
      onRetry: controller.reload,
      loading: const Padding(
        padding: EdgeInsets.all(AppTheme.gutter),
        child: Column(
          children: [
            Skeleton(height: 150),
            SizedBox(height: 10),
            Skeleton(height: 150),
          ],
        ),
      ),
      builder: (context, jobs) {
        if (jobs.isEmpty) {
          return RefreshableMessage(
            onRefresh: controller.reload,
            child: const EmptyView(
              icon: Icons.build_circle_outlined,
              title: 'Nothing to confirm',
              message: 'Reported faults and scheduled maintenance appear here.',
            ),
          );
        }

        return RefreshIndicator(
          onRefresh: controller.reload,
          child: ListView.separated(
            padding: const EdgeInsets.fromLTRB(AppTheme.gutter, 12, AppTheme.gutter, 32),
            itemCount: jobs.length,
            separatorBuilder: (_, __) => const SizedBox(height: 10),
            itemBuilder: (_, index) => _AwaitingJobCard(job: jobs[index]),
          ),
        );
      },
    );
  }
}

class _AwaitingJobCard extends StatelessWidget {
  const _AwaitingJobCard({required this.job});

  final MaintenanceSchedule job;

  static String typeLabel(MaintenanceType type) => switch (type) {
        MaintenanceType.routineService => 'Routine service',
        MaintenanceType.calibration => 'Calibration',
        MaintenanceType.repair => 'Repair',
        _ => 'Maintenance',
      };

  Future<void> _confirm(BuildContext context) async {
    final controller = context.read<MaintenanceConfirmationController>();
    final messenger = ScaffoldMessenger.of(context);

    final sure = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Confirm this job is done?'),
        content: Text(
          '${job.assetLabel} goes back into service, and any fault reported against it is closed.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(false),
            child: const Text('Cancel'),
          ),
          FilledButton(
            onPressed: () => Navigator.of(dialogContext).pop(true),
            child: const Text('Confirm done'),
          ),
        ],
      ),
    );
    if (sure != true) return;

    final refused = await controller.confirm(job);

    messenger.showSnackBar(SnackBar(
      content: Text(refused?.message ?? '${job.assetLabel} is confirmed and back in service.'),
    ));
  }

  @override
  Widget build(BuildContext context) {
    final controller = context.watch<MaintenanceConfirmationController>();
    final busy = controller.isBusy(job);
    final theme = Theme.of(context);
    final notes = job.notes;

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text(job.assetLabel, style: theme.textTheme.titleMedium),
            const SizedBox(height: 2),
            Text(
              typeLabel(job.scheduleType),
              style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant),
            ),
            const SizedBox(height: 10),
            ConfirmationDetail(
              label: 'Due',
              value: job.status == MaintenanceStatus.overdue
                  ? '${FriendlyDate.date(job.scheduledDate)} · overdue'
                  : FriendlyDate.date(job.scheduledDate),
            ),
            ConfirmationDetail(label: 'Notes', value: notes ?? 'No notes given.'),
            const SizedBox(height: 12),
            FilledButton(
              onPressed: busy ? null : () => _confirm(context),
              style: FilledButton.styleFrom(minimumSize: const Size.fromHeight(48)),
              child: busy
                  ? const SizedBox(
                      width: 18,
                      height: 18,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    )
                  : const Text('Confirm done'),
            ),
          ],
        ),
      ),
    );
  }
}
