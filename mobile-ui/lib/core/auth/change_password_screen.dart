import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../network/api_exception.dart';
import '../routing/app_router.dart';
import '../theme/app_theme.dart';
import '../widgets/phone_width.dart';
import 'auth_controller.dart';
import 'auth_form.dart';

class ChangePasswordScreen extends StatefulWidget {
  const ChangePasswordScreen({super.key, this.forced = false});

  /// Signed in with a temporary password from the hospital: nothing else is reachable until a
  /// new one is chosen, so there is no way back from here, only sign out.
  final bool forced;

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

    final screen = PhoneWidth(
      child: Scaffold(
        appBar: AppBar(
          title: Text(widget.forced ? 'Choose a new password' : 'Change password'),
          automaticallyImplyLeading: !widget.forced,
          actions: [
            if (widget.forced)
              TextButton(
                onPressed: _busy ? null : () => context.read<AuthController>().signOut(),
                child: const Text('Sign out'),
              ),
          ],
        ),
        body: ListView(
          padding: const EdgeInsets.fromLTRB(AppTheme.gutter, 12, AppTheme.gutter, 32),
          children: [
            Text(
              widget.forced
                  ? 'The hospital gave you a temporary password. Enter it as your current '
                      'password, then choose your own.'
                  : 'After changing it you will be signed out on every device, then sign in '
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

    return widget.forced ? PopScope(canPop: false, child: screen) : screen;
  }
}
