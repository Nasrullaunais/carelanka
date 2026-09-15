import 'package:flutter/material.dart';

import '../../../services/api_client/models/worklist_status.dart';

class WorklistStatusChip extends StatelessWidget {
  const WorklistStatusChip({super.key, required this.status});

  final WorklistStatus status;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final (label, background, foreground) = switch (status) {
      WorklistStatus.notArrived => ('Not arrived', scheme.surfaceContainerHighest, scheme.onSurfaceVariant),
      WorklistStatus.awaitingBed => ('Awaiting bed', scheme.tertiaryContainer, scheme.onTertiaryContainer),
      WorklistStatus.bedReady => ('Bed ready', scheme.primaryContainer, scheme.onPrimaryContainer),
      WorklistStatus.admitted => ('Admitted', scheme.secondaryContainer, scheme.onSecondaryContainer),
      WorklistStatus.completed => ('Completed', scheme.surfaceContainerHighest, scheme.onSurfaceVariant),
      WorklistStatus.cancelled => ('Cancelled', scheme.errorContainer, scheme.onErrorContainer),
      WorklistStatus.$unknown => ('Unknown', scheme.surfaceContainerHighest, scheme.onSurfaceVariant),
    };

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
      decoration: BoxDecoration(
        color: background,
        borderRadius: BorderRadius.circular(12),
      ),
      child: Text(
        label,
        style: Theme.of(context).textTheme.labelSmall?.copyWith(color: foreground),
      ),
    );
  }
}
