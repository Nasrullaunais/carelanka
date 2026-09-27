import 'package:flutter/material.dart';

import '../../../core/theme/app_theme.dart';
import '../../../core/utils/friendly_date.dart';
import '../models/my_shift_item.dart';

class AttendanceBadge extends StatelessWidget {
  const AttendanceBadge({super.key, required this.shift});

  final MyShiftItem shift;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    if (shift.isCompleted) {
      final outTime = shift.clockedOutAt != null
          ? FriendlyDate.time(shift.clockedOutAt!)
          : '';
      return _BadgePill(
        icon: Icons.check_circle_rounded,
        label: 'Completed $outTime',
        backgroundColor: theme.colorScheme.surfaceContainerHighest,
        foregroundColor: theme.colorScheme.onSurfaceVariant,
      );
    }

    if (shift.isClockedIn) {
      final inTime = shift.clockedInAt != null
          ? FriendlyDate.time(shift.clockedInAt!)
          : '';
      return _BadgePill(
        icon: Icons.login_rounded,
        label: 'Clocked In at $inTime',
        backgroundColor: const Color(0xFFE0F2F1), // Soft teal
        foregroundColor: const Color(0xFF00695C), // Brand teal
      );
    }

    return _BadgePill(
      icon: Icons.schedule_rounded,
      label: shift.status == 'confirmed' ? 'Scheduled' : shift.status,
      backgroundColor: theme.colorScheme.surfaceContainerHighest,
      foregroundColor: theme.colorScheme.onSurfaceVariant,
    );
  }
}

class _BadgePill extends StatelessWidget {
  const _BadgePill({
    required this.icon,
    required this.label,
    required this.backgroundColor,
    required this.foregroundColor,
  });

  final IconData icon;
  final String label;
  final Color backgroundColor;
  final Color foregroundColor;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
      decoration: BoxDecoration(
        color: backgroundColor,
        borderRadius: BorderRadius.circular(AppTheme.radiusM),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(icon, size: 14, color: foregroundColor),
          const SizedBox(width: 4),
          Text(
            label,
            style: TextStyle(
              fontSize: 12,
              fontWeight: FontWeight.w600,
              color: foregroundColor,
            ),
          ),
        ],
      ),
    );
  }
}
