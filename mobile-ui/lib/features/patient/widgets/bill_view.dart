import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../../core/theme/app_theme.dart';
import '../../../core/utils/friendly_date.dart';
import '../../../services/api_client/models/my_bill.dart';
import '../../../services/api_client/models/my_bill_line.dart';
import 'panels.dart';

String formatMoney(String currency, double amount) =>
    '$currency ${NumberFormat('#,##0.00').format(amount)}';

class BillView extends StatelessWidget {
  const BillView({super.key, required this.bill});

  final MyBill bill;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        if (!bill.isFinal) ...[
          NoticeBanner(
            icon: Icons.hourglass_bottom_outlined,
            accent: scheme.warning,
            title: 'Still adding up',
            body: 'Every night in a bed is added to this total, so it will keep growing '
                'until you are discharged. This is not the amount to pay yet.',
          ),
          const SizedBox(height: 16),
        ],
        for (final line in bill.lines)
          _LineRow(line: line, currency: bill.currency),
        const Divider(height: 26),
        Row(
          children: [
            Expanded(
              child: Text(
                bill.isFinal ? 'Total' : 'So far',
                style: theme.textTheme.titleMedium,
              ),
            ),
            Text(
              formatMoney(bill.currency, bill.total),
              style: theme.textTheme.titleMedium?.copyWith(fontWeight: FontWeight.w700),
            ),
          ],
        ),
        const SizedBox(height: 14),
        _Status(bill: bill),
      ],
    );
  }
}

class _LineRow extends StatelessWidget {
  const _LineRow({required this.line, required this.currency});

  final MyBillLine line;
  final String currency;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;
    final showsQuantity = line.quantity != 1;

    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 7),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(line.description, style: theme.textTheme.bodyMedium),
                if (showsQuantity) ...[
                  const SizedBox(height: 2),
                  Text(
                    '${_trimZeros(line.quantity)} x '
                    '${formatMoney(currency, line.unitPrice)}',
                    style: theme.textTheme.bodySmall
                        ?.copyWith(color: scheme.onSurfaceVariant),
                  ),
                ],
              ],
            ),
          ),
          const SizedBox(width: 12),
          Text(
            formatMoney(currency, line.lineTotal),
            style: theme.textTheme.bodyMedium?.copyWith(fontWeight: FontWeight.w500),
          ),
        ],
      ),
    );
  }

  static String _trimZeros(double value) =>
      value == value.roundToDouble() ? value.toStringAsFixed(0) : value.toString();
}

class _Status extends StatelessWidget {
  const _Status({required this.bill});

  final MyBill bill;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;
    final settledAt = bill.settledAt;

    final (icon, color, label) = bill.settled
        ? (
            Icons.check_circle_outline,
            scheme.primary,
            settledAt == null ? 'Paid' : 'Paid on ${FriendlyDate.full(settledAt)}',
          )
        : (Icons.receipt_long_outlined, scheme.onSurfaceVariant, 'Not yet paid');

    return Container(
      width: double.infinity,
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
      decoration: BoxDecoration(
        color: scheme.surfaceContainerHighest,
        borderRadius: BorderRadius.circular(AppTheme.radiusM),
      ),
      child: Row(
        children: [
          Icon(icon, size: 17, color: color),
          const SizedBox(width: 9),
          Expanded(
            child: Text(
              label,
              style: theme.textTheme.bodySmall?.copyWith(color: scheme.onSurfaceVariant),
            ),
          ),
          Text(
            bill.billNumber,
            style: theme.textTheme.labelSmall?.copyWith(
              color: scheme.onSurfaceVariant,
              fontWeight: FontWeight.w600,
            ),
          ),
        ],
      ),
    );
  }
}
