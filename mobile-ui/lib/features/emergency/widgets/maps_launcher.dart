import 'package:flutter/material.dart';
import 'package:url_launcher/url_launcher.dart';

Future<void> openInMaps(BuildContext context, String url) async {
  final messenger = ScaffoldMessenger.of(context);
  final launched = await launchUrl(Uri.parse(url), mode: LaunchMode.externalApplication).catchError((_) => false);
  if (!launched) {
    messenger.showSnackBar(const SnackBar(content: Text('No maps app could be opened on this device.')));
  }
}
