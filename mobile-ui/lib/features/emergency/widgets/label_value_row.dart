import 'package:flutter/material.dart';

class LabelValueRow extends StatelessWidget {
  const LabelValueRow({
    super.key,
    required this.label,
    required this.value,
    this.stacked = false,
  });

  final String label;
  final String? value;

  /// Puts the value under the label, for long text such as an address.
  final bool stacked;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final labelText = Text(
      label,
      style: theme.textTheme.bodyMedium?.copyWith(
        color: theme.colorScheme.onSurfaceVariant,
      ),
    );
    final valueStyle = theme.textTheme.bodyMedium?.copyWith(
      fontWeight: FontWeight.w600,
    );

    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 6),
      child: stacked
          ? Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                labelText,
                const SizedBox(height: 2),
                Text(value ?? 'Not set', style: valueStyle),
              ],
            )
          : Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                labelText,
                const SizedBox(width: 24),
                Expanded(
                  child: Text(
                    value ?? 'Not set',
                    textAlign: TextAlign.end,
                    style: valueStyle,
                  ),
                ),
              ],
            ),
    );
  }
}
