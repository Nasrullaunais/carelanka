import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../../core/auth/auth_controller.dart';
import '../../../core/auth/auth_form.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/routing/app_router.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/widgets/phone_width.dart';

class ChangePasswordScreen extends StatefulWidget {
  const ChangePasswordScreen({super.key});

  @override
  State<ChangePasswordScreen> createState() => _ChangePasswordScreenState();
}

class _ChangePasswordScreenState extends State<ChangePasswordScreen> {
  final _formKey = GlobalKey<FormState>();
  final _current = TextEditingController();
  final _new = TextEditingController();
  final _confirm = TextEditingController();

  bool _busy = false;
  String? _rejectedCurrent;
  Map<String, List<String>> _fieldErrors = const {};

  @override
  void dispose() {
    for (final controller in [_current, _new, _confirm]) {
      controller.dispose();
    }
    super.dispose();
  }

  Future<void> _submit() async {
    setState(() => _fieldErrors = const {});
    if (!_formKey.currentState!.validate()) return;

    // Read before the call: a successful change signs out, which unmounts this screen.
    final auth = context.read<AuthController>();
    final router = GoRouter.of(context);
    final messenger = ScaffoldMessenger.of(context);

    setState(() => _busy = true);

    try {
      await auth.changePassword(currentPassword: _current.text, newPassword: _new.text);
    } on ApiException catch (error) {
      if (!mounted) return;
      setState(() => _busy = false);

      if (error.code == AuthController.currentPasswordIncorrectCode) {
        _rejectedCurrent = _current.text;
        _formKey.currentState!.validate();
      } else if (error.fieldErrors.isNotEmpty) {
        setState(() => _fieldErrors = error.fieldErrors);
      } else {
        messenger.showSnackBar(SnackBar(content: Text(error.message)));
      }
      return;
    }

    messenger.showSnackBar(
      const SnackBar(content: Text('Password changed. Sign in with your new password.')),
    );
    router.go(AppRoutes.patientLogin);
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return PhoneWidth(
      child: Scaffold(
        appBar: AppBar(title: const Text('Change password')),
        body: ListView(
          padding: const EdgeInsets.fromLTRB(AppTheme.gutter, 12, AppTheme.gutter, 32),
          children: [
            Text(
              'After changing it you will be signed out on every device, then sign in '
              'again with the new password.',
              style: theme.textTheme.bodyMedium?.copyWith(
                color: theme.colorScheme.onSurfaceVariant,
              ),
            ),
            const SizedBox(height: 20),
            Form(
              key: _formKey,
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  AuthPasswordField(
                    controller: _current,
                    label: 'Current password',
                    enabled: !_busy,
                    serverErrors: _fieldErrors['current_password'],
                    validator: (value) {
                      if (value == null || value.isEmpty) return 'Enter your current password';
                      if (value == _rejectedCurrent) return 'This is not your current password';
                      return null;
                    },
                  ),
                  AuthPasswordField(
                    controller: _new,
                    label: 'New password',
                    enabled: !_busy,
                    serverErrors: _fieldErrors['new_password'],
                    validator: (value) => value == _current.text
                        ? 'Choose a password different from your current one'
                        : validatePassword(value),
                  ),
                  AuthPasswordField(
                    controller: _confirm,
                    label: 'Confirm new password',
                    enabled: !_busy,
                    textInputAction: TextInputAction.done,
                    onSubmitted: (_) => _busy ? null : _submit(),
                    validator: (value) => value != _new.text ? 'Passwords do not match' : null,
                  ),
                  const SizedBox(height: 8),
                  AuthSubmitButton(label: 'Change password', busy: _busy, onPressed: _submit),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}
