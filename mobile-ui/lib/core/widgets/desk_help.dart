import 'package:flutter/material.dart';

import '../config/hospital_contact.dart';
import '../theme/app_theme.dart';
import '../utils/dialer.dart';
import 'notice_banner.dart';

class DeskHelp extends StatelessWidget {
  const DeskHelp({super.key, required this.title, required this.body});

  final String title;
  final String body;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;

    return NoticeBanner(
      icon: Icons.support_agent_outlined,
      accent: scheme.warning,
      title: title,
      body: body,
      action: Wrap(
        spacing: 8,
        runSpacing: 8,
        children: [
          OutlinedButton.icon(
            onPressed: () => callNumber(context, HospitalContact.reception),
            icon: const Icon(Icons.call_outlined, size: 18),
            label: const Text(HospitalContact.reception),
            style: OutlinedButton.styleFrom(minimumSize: const Size(0, 42)),
          ),
          OutlinedButton.icon(
            onPressed: () => emailAddress(context, HospitalContact.email),
            icon: const Icon(Icons.mail_outline, size: 18),
            label: const Text(HospitalContact.email),
            style: OutlinedButton.styleFrom(minimumSize: const Size(0, 42)),
          ),
        ],
      ),
    );
  }
}

/// The one place both "Forgot password?" on sign-in and "Reset password" in Manage account open.
Future<void> showResetPasswordHelp(BuildContext context) {
  return showModalBottomSheet<void>(
    context: context,
    isScrollControlled: true,
    showDragHandle: true,
    builder: (_) => const SafeArea(
      child: Padding(
        padding: EdgeInsets.fromLTRB(AppTheme.gutter, 0, AppTheme.gutter, 24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            DeskHelp(
              title: 'Reset your password',
              body: 'Call or email the hospital. After they check who you are, they will give '
                  'you a new password to sign in with. You can change it after.',
            ),
          ],
        ),
      ),
    ),
  );
}
