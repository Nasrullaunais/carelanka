import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:qr_flutter/qr_flutter.dart';

import '../../../core/theme/app_theme.dart';

/// The patient code, big enough to read out and scannable at the desk.
///
/// Deliberately a plain card. It was a second teal gradient at first, stacked
/// under the one on the card above it, and two coloured blocks in a row means
/// neither one is the thing you look at. A scanner also copes better with a
/// black code on white than on a tinted panel.
class PatientIdCard extends StatelessWidget {
  const PatientIdCard({super.key, required this.patientCode, required this.fullName});

  final String patientCode;
  final String fullName;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;

    return Container(
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        color: scheme.surface,
        borderRadius: BorderRadius.circular(AppTheme.radiusL),
        border: Border.all(color: scheme.outlineVariant.withValues(alpha: 0.6)),
      ),
      child: Row(
        children: [
          Container(
            padding: const EdgeInsets.all(8),
            decoration: BoxDecoration(
              color: Colors.white,
              borderRadius: BorderRadius.circular(AppTheme.radiusS),
              border: Border.all(color: scheme.outlineVariant),
            ),
            child: QrImageView(
              data: patientCode,
              size: 76,
              padding: EdgeInsets.zero,
              backgroundColor: Colors.white,
            ),
          ),
          const SizedBox(width: 16),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'Show this at reception',
                  style: theme.textTheme.labelSmall?.copyWith(
                    color: scheme.onSurfaceVariant,
                    letterSpacing: 0.6,
                  ),
                ),
                const SizedBox(height: 6),
                Text(
                  patientCode,
                  style: theme.textTheme.titleLarge?.copyWith(
                    color: scheme.onSurface,
                    fontWeight: FontWeight.w700,
                    letterSpacing: 1.6,
                  ),
                ),
                const SizedBox(height: 2),
                Text(
                  fullName,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: theme.textTheme.bodySmall
                      ?.copyWith(color: scheme.onSurfaceVariant),
                ),
              ],
            ),
          ),
          IconButton(
            tooltip: 'Copy code',
            onPressed: () {
              Clipboard.setData(ClipboardData(text: patientCode));
              ScaffoldMessenger.of(context).showSnackBar(
                const SnackBar(content: Text('Patient code copied.')),
              );
            },
            icon: Icon(Icons.copy_rounded, color: scheme.onSurfaceVariant, size: 20),
          ),
        ],
      ),
    );
  }
}
