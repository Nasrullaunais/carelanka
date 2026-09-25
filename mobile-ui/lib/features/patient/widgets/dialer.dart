import 'package:flutter/material.dart';
import 'package:url_launcher/url_launcher.dart';

// Opens the dialler with the number filled in — doesn't place the call itself.
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

// Opens the mail app with the address filled in.
Future<void> emailAddress(BuildContext context, String address) async {
  final messenger = ScaffoldMessenger.of(context);
  final uri = Uri(scheme: 'mailto', path: address);

  final launched = await launchUrl(uri).catchError((_) => false);
  if (!launched) {
    messenger.showSnackBar(
      SnackBar(content: Text('No mail app is set up. The address is $address.')),
    );
  }
}
