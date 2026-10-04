import 'package:flutter/material.dart';

import '../../../core/theme/app_theme.dart';
import '../../../services/api_client/models/dispatch_detail.dart';
import '../models/run_step.dart';
import 'label_value_row.dart';

class RunCard extends StatelessWidget {
  const RunCard({
    super.key,
    required this.run,
    required this.busy,
    required this.onStep,
    required this.onDecline,
    required this.onNavigate,
    this.onEndAtScene,
    this.onCallCaller,
  });

  final DispatchDetail run;
  final bool busy;
  final VoidCallback onStep;
  final VoidCallback onDecline;
  final VoidCallback onNavigate;
  final VoidCallback? onEndAtScene;
  final ValueChanged<String>? onCallCaller;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final status = run.status;
    final step = status?.nextStep;
    final phone = run.callerPhone?.trim();

    return ListView(
      padding: const EdgeInsets.all(AppTheme.gutter),
      children: [
        Text(status?.crewLabel ?? '', style: theme.textTheme.headlineSmall),
        const SizedBox(height: 4),
        Text(
          '${run.callPriority?.label ?? 'Unknown'} priority',
          style: theme.textTheme.bodyLarge?.copyWith(
            color: theme.colorScheme.onSurfaceVariant,
          ),
        ),
        const SizedBox(height: 16),
        Card(
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text('The emergency', style: theme.textTheme.titleMedium),
                const SizedBox(height: 8),
                Text(
                  run.callDetails?.trim().isNotEmpty == true
                      ? run.callDetails!
                      : 'No details were given by the caller.',
                ),
                if (run.patientName?.trim().isNotEmpty == true)
                  LabelValueRow(label: 'Patient', value: run.patientName),
                if (run.callerName?.trim().isNotEmpty == true)
                  LabelValueRow(label: 'Caller', value: run.callerName),
                if (phone != null && phone.isNotEmpty && onCallCaller != null)
                  Padding(
                    padding: const EdgeInsets.only(top: 8),
                    child: OutlinedButton.icon(
                      onPressed: () => onCallCaller!(phone),
                      icon: const Icon(Icons.phone_outlined),
                      label: Text('Call the caller · $phone'),
                    ),
                  ),
              ],
            ),
          ),
        ),
        Card(
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                LabelValueRow(
                  label: 'Ambulance',
                  value: run.ambulanceRegistration,
                ),
                if (run.sceneAddressLabel?.trim().isNotEmpty == true)
                  LabelValueRow(label: 'Scene', value: run.sceneAddressLabel),
                if (run.destinationLabel?.trim().isNotEmpty == true &&
                    run.destinationLabel != run.sceneAddressLabel)
                  LabelValueRow(label: 'Going to', value: run.destinationLabel),
                LabelValueRow(
                  label: 'Crew on board',
                  value: run.crewCount?.toString(),
                ),
              ],
            ),
          ),
        ),
        const SizedBox(height: 24),
        if (step != null)
          FilledButton(
            onPressed: busy ? null : onStep,
            style: FilledButton.styleFrom(
              minimumSize: const Size.fromHeight(56),
            ),
            child: busy
                ? const SizedBox.square(
                    dimension: 22,
                    child: CircularProgressIndicator(strokeWidth: 2),
                  )
                : Text(step.label),
          ),
        if ((status?.canEndAtScene ?? false) && onEndAtScene != null) ...[
          const SizedBox(height: 12),
          OutlinedButton(
            onPressed: busy ? null : onEndAtScene,
            style: OutlinedButton.styleFrom(
              minimumSize: const Size.fromHeight(52),
            ),
            child: const Text('End without going to hospital'),
          ),
        ],
        if (status?.canNavigate ?? false) ...[
          const SizedBox(height: 12),
          OutlinedButton.icon(
            onPressed: busy ? null : onNavigate,
            icon: const Icon(Icons.navigation_outlined),
            label: const Text('Open in Google Maps'),
            style: OutlinedButton.styleFrom(
              minimumSize: const Size.fromHeight(52),
            ),
          ),
        ],
        if (status?.canDecline ?? false) ...[
          const SizedBox(height: 12),
          TextButton(
            onPressed: busy ? null : onDecline,
            child: const Text('I cannot take this run'),
          ),
        ],
      ],
    );
  }
}
