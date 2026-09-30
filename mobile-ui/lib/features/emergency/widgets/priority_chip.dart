import 'package:flutter/material.dart';

import '../../../services/api_client/models/call_priority.dart';
import '../models/call_priority_labels.dart';
import 'crew_colors.dart';

class PriorityChip extends StatelessWidget {
  const PriorityChip({super.key, required this.priority});

  final CallPriority priority;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final (background, foreground) = switch (priority) {
      CallPriority.critical => (scheme.errorContainer, scheme.onErrorContainer),
      CallPriority.high => (CrewColors.amber, CrewColors.onAmber),
      CallPriority.medium => (
        scheme.tertiaryContainer,
        scheme.onTertiaryContainer,
      ),
      _ => (scheme.secondaryContainer, scheme.onSecondaryContainer),
    };

    return Semantics(
      label: 'Priority: ${priority.crewLabel}',
      excludeSemantics: true,
      child: DecoratedBox(
        decoration: BoxDecoration(
          color: background,
          borderRadius: BorderRadius.circular(999),
        ),
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
          child: Text(
            priority.crewLabel,
            style: Theme.of(context).textTheme.labelLarge?.copyWith(
              color: foreground,
              fontWeight: FontWeight.w700,
            ),
          ),
        ),
      ),
    );
  }
}
