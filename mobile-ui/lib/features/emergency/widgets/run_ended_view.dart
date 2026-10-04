import 'package:flutter/material.dart';

import '../../../core/theme/app_theme.dart';
import '../../../services/api_client/models/dispatch_detail.dart';
import '../../../services/api_client/models/dispatch_status.dart';

/// Shown in place of the run that just ended, so a crew halfway to a scene
/// learns they can stop instead of finding an empty screen.
class RunEndedView extends StatelessWidget {
  const RunEndedView({super.key, required this.run, required this.onDismiss});

  final DispatchDetail run;
  final VoidCallback onDismiss;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final (icon, title, message, reason) = _explain(run);
    final stop =
        run.status == DispatchStatus.cancelled ||
        run.status == DispatchStatus.reassigned;

    return ListView(
      padding: const EdgeInsets.all(AppTheme.gutter),
      children: [
        Icon(
          icon,
          size: 56,
          color: stop ? theme.colorScheme.error : theme.colorScheme.primary,
        ),
        const SizedBox(height: 12),
        Text(
          title,
          style: theme.textTheme.headlineSmall,
          textAlign: TextAlign.center,
        ),
        const SizedBox(height: 8),
        Text(message, textAlign: TextAlign.center),
        if (reason != null && reason.trim().isNotEmpty) ...[
          const SizedBox(height: 16),
          Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('Reason given', style: theme.textTheme.labelLarge),
                  const SizedBox(height: 4),
                  Text(reason),
                ],
              ),
            ),
          ),
        ],
        const SizedBox(height: 24),
        FilledButton(
          onPressed: onDismiss,
          style: FilledButton.styleFrom(minimumSize: const Size.fromHeight(52)),
          child: const Text('OK'),
        ),
      ],
    );
  }

  static (IconData, String, String, String?) _explain(DispatchDetail run) {
    final ambulance = run.ambulanceRegistration ?? 'your ambulance';
    return switch (run.status) {
      DispatchStatus.cancelled => (
        Icons.cancel_outlined,
        'Run called off',
        'The duty manager called off this run. You can stop and wait for the next call.',
        run.cancellationReason,
      ),
      DispatchStatus.reassigned => (
        Icons.swap_horiz,
        'Run given to another ambulance',
        'Another ambulance is taking this call instead of $ambulance. You can stop.',
        run.reassignmentReason,
      ),
      DispatchStatus.declined => (
        Icons.undo,
        'You declined this run',
        'The duty manager will send a different ambulance.',
        run.declinedReason,
      ),
      DispatchStatus.closedAtScene => (
        Icons.check_circle_outline,
        'Run ended at the scene',
        '$ambulance is free for the next call.',
        null,
      ),
      _ => (
        Icons.check_circle_outline,
        'Run complete',
        'The handover is recorded and $ambulance is free for the next call.',
        null,
      ),
    };
  }
}
