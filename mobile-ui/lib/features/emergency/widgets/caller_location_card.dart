import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../state/caller_location_controller.dart';

class CallerLocationCard extends StatelessWidget {
  const CallerLocationCard({super.key});

  @override
  Widget build(BuildContext context) {
    final location = context.watch<CallerLocationController>();
    final scheme = Theme.of(context).colorScheme;
    final fix = location.fix;

    final (
      IconData icon,
      String title,
      String? body,
      bool warn,
    ) = switch (location.status) {
      CallerLocationStatus.checking => (
        Icons.my_location,
        'Checking location access…',
        null,
        false,
      ),
      CallerLocationStatus.needsPermission => (
        Icons.location_disabled_outlined,
        'Allow location so the ambulance can find you',
        'CareLanka only uses your location while this screen is open.',
        true,
      ),
      CallerLocationStatus.blocked => (
        Icons.location_disabled_outlined,
        'Location is blocked for CareLanka',
        'Open Settings, go to Permissions → Location, and choose '
            '"Allow only while using the app" with "Use precise location" on.',
        true,
      ),
      CallerLocationStatus.serviceOff => (
        Icons.location_off_outlined,
        "Your phone's location is turned off",
        'Turn it on so we can find where you are.',
        true,
      ),
      CallerLocationStatus.searching => (
        Icons.my_location,
        'Finding your location…',
        null,
        false,
      ),
      CallerLocationStatus.slow => (
        Icons.my_location,
        'Still looking for your location…',
        'If you can, move near a window or step outside.',
        true,
      ),
      CallerLocationStatus.unavailable => (
        Icons.location_off_outlined,
        'We could not get your location',
        null,
        true,
      ),
      CallerLocationStatus.located when fix != null => (
        Icons.location_on,
        'Location found',
        'Accurate to about ${_distance(fix.accuracyMetres)}.',
        location.isWeak,
      ),
      CallerLocationStatus.located => (
        Icons.my_location,
        'Finding your location…',
        null,
        false,
      ),
    };

    final action = _action(location);
    final busy =
        location.status == CallerLocationStatus.checking ||
        location.status == CallerLocationStatus.searching ||
        location.status == CallerLocationStatus.slow;

    return Card(
      color: warn ? scheme.errorContainer : null,
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                busy
                    ? const SizedBox.square(
                        dimension: 20,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    : Icon(icon, color: warn ? scheme.onErrorContainer : null),
                const SizedBox(width: 12),
                Expanded(
                  child: Text(
                    title,
                    style: Theme.of(context).textTheme.titleSmall,
                  ),
                ),
              ],
            ),
            if (body != null) ...[const SizedBox(height: 6), Text(body)],
            if (location.approximate && fix != null) ...[
              const SizedBox(height: 6),
              const Text(
                'Your phone is only sharing an approximate location. '
                'The ambulance may struggle to find you.',
              ),
            ] else if (location.isWeak) ...[
              const SizedBox(height: 6),
              const Text(
                'The signal is weak. If you can, move near a window or step '
                'outside. You can still send the request now.',
              ),
            ],
            if (action != null) ...[
              const SizedBox(height: 12),
              OutlinedButton(onPressed: action.$2, child: Text(action.$1)),
            ],
          ],
        ),
      ),
    );
  }

  static (String, VoidCallback)? _action(CallerLocationController location) {
    if (location.approximate && location.fix != null) {
      return location.preciseRefused
          ? ('Open settings', location.openAppSettings)
          : ('Use precise location', location.askForPrecise);
    }
    return switch (location.status) {
      CallerLocationStatus.needsPermission => (
        'Allow location',
        location.start,
      ),
      CallerLocationStatus.blocked => (
        'Open settings',
        location.openAppSettings,
      ),
      CallerLocationStatus.serviceOff => (
        'Turn on location',
        location.openLocationSettings,
      ),
      CallerLocationStatus.unavailable => ('Try again', location.retry),
      _ => null,
    };
  }

  static String _distance(double metres) => metres < 1000
      ? '${metres.round()} m'
      : '${(metres / 1000).toStringAsFixed(1)} km';
}
