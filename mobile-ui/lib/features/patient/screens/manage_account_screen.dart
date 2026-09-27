import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/auth/auth_controller.dart';
import '../../../core/auth/change_password_screen.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/widgets/desk_help.dart';
import '../../../core/widgets/phone_width.dart';

class ManageAccountScreen extends StatelessWidget {
  const ManageAccountScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final username = context.watch<AuthController>().principal?.displayName ?? '';

    return PhoneWidth(
      child: Scaffold(
        appBar: AppBar(title: const Text('Manage account')),
        body: ListView(
          padding: const EdgeInsets.fromLTRB(AppTheme.gutter, 12, AppTheme.gutter, 32),
          children: [
            Card(
              child: Column(
                children: [
                  ListTile(
                    leading: const Icon(Icons.person_outline),
                    title: const Text('Username'),
                    subtitle: Text(username),
                  ),
                  const Divider(indent: 20, endIndent: 20),
                  ListTile(
                    leading: const Icon(Icons.lock_outline),
                    title: const Text('Change password'),
                    trailing: const Icon(Icons.chevron_right),
                    onTap: () => Navigator.of(context).push(
                      MaterialPageRoute(builder: (_) => const ChangePasswordScreen()),
                    ),
                  ),
                  const Divider(indent: 20, endIndent: 20),
                  ListTile(
                    leading: const Icon(Icons.lock_reset_outlined),
                    title: const Text('Reset password'),
                    subtitle: const Text('Forgot it? The hospital can give you a new one.'),
                    trailing: const Icon(Icons.chevron_right),
                    onTap: () => showResetPasswordHelp(context),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}
