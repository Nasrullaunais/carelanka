import 'package:flutter/material.dart';

import '../../../core/theme/app_theme.dart';
import '../../../services/api_client/models/dispatch_detail.dart';
import '../models/run_step.dart';

class RunCard extends StatelessWidget {
  const RunCard({
    super.key,
    required this.run,
    required this.busy,
    required this.onStep,
    required this.onDecline,
    required this.onNavigate,
  });

  final DispatchDetail run;
  final bool busy;
  final VoidCallback onStep;
  final VoidCallback onDecline;
  final VoidCallback onNavigate;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final status = run.status;
    final step = status?.nextStep;

    return ListView(
      padding: const EdgeInsets.all(AppTheme.gutter),
      children: [
        Text(status?.crewLabel ?? '', style: theme.textTheme.headlineSmall),
        const SizedBox(height: 4),
        Text(
          'Priority: ${run.callPriority?.name ?? 'unknown'}',
          style: theme.textTheme.bodyLarge?.copyWith(color: theme.colorScheme.onSurfaceVariant),
        ),
        const SizedBox(height: 16),
        Card(
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                _Row(label: 'Ambulance', value: run.ambulanceRegistration),
                _Row(label: 'Crew on board', value: run.crewCount?.toString()),
                _Row(label: 'Going to ward', value: run.destinationWardName),
              ],
            ),
          ),
        ),
        const SizedBox(height: 24),
        if (step != null)
          FilledButton(
            onPressed: busy ? null : onStep,
            style: FilledButton.styleFrom(minimumSize: const Size.fromHeight(56)),
            child: busy ? const SizedBox.square(dimension: 22, child: CircularProgressIndicator(strokeWidth: 2)) : Text(step.label),
          ),
        if (status?.canNavigate ?? false) ...[
          const SizedBox(height: 12),
          OutlinedButton.icon(
            onPressed: busy ? null : onNavigate,
            icon: const Icon(Icons.navigation_outlined),
            label: const Text('Open in Google Maps'),
            style: OutlinedButton.styleFrom(minimumSize: const Size.fromHeight(52)),
          ),
        ],
        if (status?.canDecline ?? false) ...[
          const SizedBox(height: 12),
          TextButton(onPressed: busy ? null : onDecline, child: const Text('I cannot take this run')),
        ],
      ],
    );
  }
}

class _Row extends StatelessWidget {
  const _Row({required this.label, required this.value});

  final String label;
  final String? value;

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.symmetric(vertical: 4),
        child: Row(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          children: [Text(label), Flexible(child: Text(value ?? 'Not set', textAlign: TextAlign.end))],
        ),
      );
}
