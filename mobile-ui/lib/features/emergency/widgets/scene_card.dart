import 'package:flutter/material.dart';

import '../../../core/theme/app_theme.dart';
import '../../../core/utils/dialer.dart';
import '../../../services/api_client/models/dispatch_detail.dart';

const approximateLocationMetres = 50;

class SceneCard extends StatelessWidget {
  const SceneCard({super.key, required this.run});

  final DispatchDetail run;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;
    final details = run.sceneDetails?.trim();
    final accuracy = run.sceneLocationAccuracyMetres;
    final phone = run.callerPhone?.trim();

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'Scene',
              style: theme.textTheme.labelLarge?.copyWith(
                color: scheme.onSurfaceVariant,
              ),
            ),
            if (details != null && details.isNotEmpty) ...[
              const SizedBox(height: 8),
              Text('“$details”', style: theme.textTheme.bodyLarge),
            ],
            const SizedBox(height: 8),
            _Address(run: run),
            if (accuracy != null && accuracy > approximateLocationMetres) ...[
              const SizedBox(height: 12),
              _ApproximateWarning(
                metres: accuracy,
                canCall: phone != null && phone.isNotEmpty,
              ),
            ],
            const Padding(
              padding: EdgeInsets.symmetric(vertical: 12),
              child: Divider(height: 1),
            ),
            _Caller(run: run),
          ],
        ),
      ),
    );
  }
}

class _Address extends StatelessWidget {
  const _Address({required this.run});

  final DispatchDetail run;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final label = run.sceneAddressLabel?.trim();
    if (label != null && label.isNotEmpty) {
      return Text(label, style: theme.textTheme.titleMedium);
    }
    final latitude = run.sceneLatitude;
    final longitude = run.sceneLongitude;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        if (latitude != null && longitude != null)
          Text(
            '${latitude.toStringAsFixed(5)}, ${longitude.toStringAsFixed(5)}',
            style: theme.textTheme.titleMedium,
          ),
        Text(
          'Street name not available yet',
          style: theme.textTheme.bodyMedium?.copyWith(
            color: theme.colorScheme.onSurfaceVariant,
          ),
        ),
      ],
    );
  }
}

class _ApproximateWarning extends StatelessWidget {
  const _ApproximateWarning({required this.metres, required this.canCall});

  final double metres;
  final bool canCall;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;
    final advice = canCall
        ? 'Call the caller to confirm the exact spot.'
        : 'Confirm the exact spot when you arrive.';

    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: scheme.warningSurface,
        borderRadius: BorderRadius.circular(AppTheme.radiusM),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(Icons.warning_amber_rounded, color: scheme.warning, size: 20),
          const SizedBox(width: 10),
          Expanded(
            child: Text(
              'Location is approximate (±${metres.round()} m). $advice',
              style: theme.textTheme.bodyMedium?.copyWith(
                color: scheme.warning,
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _Caller extends StatelessWidget {
  const _Caller({required this.run});

  final DispatchDetail run;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final muted = theme.colorScheme.onSurfaceVariant;
    final name = run.callerName?.trim();
    final phone = run.callerPhone?.trim();
    final hasName = name != null && name.isNotEmpty;
    final hasPhone = phone != null && phone.isNotEmpty;

    if (!hasName && !hasPhone) {
      return Text(
        'No caller details recorded',
        style: theme.textTheme.bodyMedium?.copyWith(color: muted),
      );
    }

    final role = switch (run.patientIsCaller) {
      true => ' · the patient',
      false => ' · for someone else',
      null => '',
    };

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          hasName ? 'Caller: $name$role' : 'Caller$role',
          style: theme.textTheme.bodyLarge,
        ),
        const SizedBox(height: 4),
        if (hasPhone) ...[
          Text(
            phone,
            style: theme.textTheme.bodyMedium?.copyWith(color: muted),
          ),
          const SizedBox(height: 12),
          FilledButton.tonalIcon(
            onPressed: () => callNumber(context, phone),
            icon: const Icon(Icons.call_outlined),
            label: const Text('Call caller'),
            style: FilledButton.styleFrom(
              minimumSize: const Size.fromHeight(48),
            ),
          ),
        ] else
          Text(
            'No phone number recorded',
            style: theme.textTheme.bodyMedium?.copyWith(color: muted),
          ),
      ],
    );
  }
}
