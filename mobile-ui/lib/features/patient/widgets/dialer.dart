import 'package:flutter/material.dart';
import 'package:url_launcher/url_launcher.dart';

/// Hands a number to the phone's dialler.
///
/// It does not place the call — it opens the dialler with the number filled in,
/// so the last tap is always the person's. On a browser this may do nothing at
/// all, which is why the failure is reported rather than swallowed.
Future<void> callNumber(BuildContext context, String number) async {
  final messenger = ScaffoldMessenger.of(context);
  final uri = Uri(scheme: 'tel', path: number.replaceAll(RegExp(r'[^\d+]'), ''));

  final launched = await launchUrl(uri).catchError((_) => false);
  if (!launched) {
    messenger.showSnackBar(
      SnackBar(content: Text('This device cannot place calls. The number is $number.')),
    );
  }
}
