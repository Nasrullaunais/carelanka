import 'package:flutter/material.dart';

import '../../../core/theme/app_theme.dart';
import '../../../core/utils/friendly_date.dart';
import '../models/staff_leave_item.dart';

class LeaveRequestCard extends StatelessWidget {
  const LeaveRequestCard({
    super.key,
    required this.item,
    required this.isActionBusy,
    required this.onWithdraw,
  });

  final StaffLeaveItem item;
  final bool isActionBusy;
  final VoidCallback onWithdraw;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final sDate = item.parsedStartDate != null
        ? FriendlyDate.dayAndMonth(item.parsedStartDate!)
        : item.startDate;
    final eDate = item.parsedEndDate != null
        ? FriendlyDate.dayAndMonth(item.parsedEndDate!)
        : item.endDate;

    final typeIcon = switch (item.type) {
      'annual' => Icons.beach_access_rounded,
      'sick' => Icons.sick_rounded,
      'emergency' => Icons.warning_amber_rounded,
      'shift_swap' => Icons.swap_horiz_rounded,
      _ => Icons.event_note_rounded,
    };

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Container(
                  padding: const EdgeInsets.all(8),
                  decoration: BoxDecoration(
                    color: theme.colorScheme.primaryContainer,
                    shape: BoxShape.circle,
                  ),
                  child: Icon(
                    typeIcon,
                    size: 20,
                    color: theme.colorScheme.onPrimaryContainer,
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Row(
                        children: [
                          Expanded(
                            child: Text(
                              item.typeDisplay,
                              style: theme.textTheme.titleMedium,
                            ),
                          ),
                          _StatusBadge(status: item.status, statusDisplay: item.statusDisplay),
                        ],
                      ),
                      const SizedBox(height: 4),
                      Text(
                        '$sDate – $eDate (${item.totalDays} ${item.totalDays == 1 ? 'day' : 'days'})',
                        style: theme.textTheme.bodyMedium?.copyWith(
                          color: theme.colorScheme.onSurfaceVariant,
                          fontWeight: FontWeight.w500,
                        ),
                      ),
                    ],
                  ),
                ),
              ],
            ),
            if (item.isUrgent) ...[
              const SizedBox(height: 8),
              Row(
                children: [
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
                    decoration: BoxDecoration(
                      color: theme.colorScheme.errorContainer,
                      borderRadius: BorderRadius.circular(AppTheme.radiusS),
                    ),
                    child: Text(
                      'URGENT',
                      style: TextStyle(
                        fontSize: 11,
                        fontWeight: FontWeight.w700,
                        color: theme.colorScheme.onErrorContainer,
                      ),
                    ),
                  ),
                ],
              ),
            ],
            if (item.reason != null && item.reason!.isNotEmpty) ...[
              const SizedBox(height: 10),
              Container(
                padding: const EdgeInsets.all(10),
                decoration: BoxDecoration(
                  color: theme.colorScheme.surfaceContainerHighest.withValues(alpha: 0.5),
                  borderRadius: BorderRadius.circular(AppTheme.radiusM),
                ),
                child: Text(
                  item.reason!,
                  style: theme.textTheme.bodyMedium,
                ),
              ),
            ],
            if (item.reviewNotes != null && item.reviewNotes!.isNotEmpty) ...[
              const SizedBox(height: 8),
              Text(
                'Note from administrator: ${item.reviewNotes}',
                style: theme.textTheme.bodySmall?.copyWith(
                  fontStyle: FontStyle.italic,
                  color: theme.colorScheme.onSurfaceVariant,
                ),
              ),
            ],
            if (item.affectedShiftsCount > 0) ...[
              const SizedBox(height: 8),
              Row(
                children: [
                  Icon(Icons.info_outline_rounded, size: 14, color: theme.colorScheme.primary),
                  const SizedBox(width: 4),
                  Text(
                    'Affects ${item.affectedShiftsCount} scheduled ${item.affectedShiftsCount == 1 ? 'shift' : 'shifts'}',
                    style: theme.textTheme.bodySmall?.copyWith(
                      color: theme.colorScheme.primary,
                      fontWeight: FontWeight.w500,
                    ),
                  ),
                ],
              ),
            ],
            if (item.canWithdraw) ...[
              const SizedBox(height: 12),
              Align(
                alignment: Alignment.centerRight,
                child: OutlinedButton.icon(
                  onPressed: isActionBusy ? null : onWithdraw,
                  icon: isActionBusy
                      ? const SizedBox(
                          width: 14,
                          height: 14,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        )
                      : const Icon(Icons.cancel_outlined, size: 16),
                  label: const Text('Withdraw Request'),
                  style: OutlinedButton.styleFrom(
                    visualDensity: VisualDensity.compact,
                    foregroundColor: theme.colorScheme.error,
                    side: BorderSide(color: theme.colorScheme.error.withValues(alpha: 0.5)),
                  ),
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

class _StatusBadge extends StatelessWidget {
  const _StatusBadge({required this.status, required this.statusDisplay});

  final String status;
  final String statusDisplay;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final (bg, fg) = switch (status) {
      'approved' => (const Color(0xFFE8F5E9), const Color(0xFF2E7D32)),
      'rejected' => (theme.colorScheme.errorContainer, theme.colorScheme.onErrorContainer),
      'pending' => (const Color(0xFFFFF3E0), const Color(0xFFE65100)),
      'withdrawn' => (theme.colorScheme.surfaceContainerHighest, theme.colorScheme.onSurfaceVariant),
      _ => (theme.colorScheme.surfaceContainerHighest, theme.colorScheme.onSurfaceVariant),
    };

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
      decoration: BoxDecoration(
        color: bg,
        borderRadius: BorderRadius.circular(AppTheme.radiusM),
      ),
      child: Text(
        statusDisplay,
        style: TextStyle(
          fontSize: 12,
          fontWeight: FontWeight.w600,
          color: fg,
        ),
      ),
    );
  }
}
