import 'package:flutter/material.dart';

import '../../../core/theme/app_theme.dart';
import '../../../services/api_client/models/call_priority.dart';
import '../models/run_step.dart';

class PriorityPill extends StatelessWidget {
  const PriorityPill({super.key, required this.priority});

  final CallPriority? priority;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;
    final color = switch (priority) {
      CallPriority.critical || CallPriority.high => scheme.error,
      CallPriority.medium => scheme.warning,
      _ => scheme.muted,
    };

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.14),
        borderRadius: BorderRadius.circular(AppTheme.radiusS),
        border: Border.all(color: color.withValues(alpha: 0.35)),
      ),
      child: Text(
        '${priority?.label ?? 'Unknown'} priority',
        style: theme.textTheme.labelMedium?.copyWith(color: color),
      ),
    );
  }
}
