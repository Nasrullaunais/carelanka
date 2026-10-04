import 'package:flutter/material.dart';
import 'package:url_launcher/url_launcher.dart';

Future<void> callPhone(BuildContext context, String number) async {
  final messenger = ScaffoldMessenger.of(context);
  final launched = await launchUrl(
    Uri(scheme: 'tel', path: number),
  ).catchError((_) => false);
  if (!launched) {
    messenger.showSnackBar(
      const SnackBar(content: Text('This phone cannot make calls.')),
    );
  }
}

/// Offered when the app cannot send a request, so the caller is never left
/// with no way to get an ambulance.
class NationalAmbulanceLine extends StatelessWidget {
  const NationalAmbulanceLine({super.key});

  static const number = '1990';

  @override
  Widget build(BuildContext context) => Card(
    child: Padding(
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            'Cannot send the request?',
            style: Theme.of(context).textTheme.titleSmall,
          ),
          const SizedBox(height: 4),
          const Text(
            'Call 1990, the free Suwa Seriya ambulance service. It works '
            'without the app or the internet.',
          ),
          const SizedBox(height: 12),
          OutlinedButton.icon(
            onPressed: () => callPhone(context, number),
            icon: const Icon(Icons.call),
            label: const Text('Call 1990'),
          ),
        ],
      ),
    ),
  );
}
