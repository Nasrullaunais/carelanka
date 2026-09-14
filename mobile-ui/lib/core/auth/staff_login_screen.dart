import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import 'auth_controller.dart';
import 'auth_form.dart';

class StaffLoginScreen extends StatefulWidget {
  const StaffLoginScreen({super.key});

  @override
  State<StaffLoginScreen> createState() => _StaffLoginScreenState();
}

class _StaffLoginScreenState extends State<StaffLoginScreen> {
  final _formKey = GlobalKey<FormState>();
  final _email = TextEditingController();
  final _password = TextEditingController();

  bool _busy = false;
  bool _obscure = true;

  @override
  void dispose() {
    _email.dispose();
    _password.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;

    setState(() => _busy = true);
    final auth = context.read<AuthController>();
    final signedIn = await auth.signInAsStaff(
      email: _email.text.trim(),
      password: _password.text,
    );

    if (!mounted) return;
    setState(() => _busy = false);

    if (!signedIn) {
      final message = auth.lastError?.message ?? 'Could not sign in.';
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(message)));
    }
  }

  @override
  Widget build(BuildContext context) {
    return AuthScaffold(
      title: 'Staff sign in',
      subtitle: 'Use your hospital email address.',
      children: [
        Form(
          key: _formKey,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              AuthTextField(
                controller: _email,
                label: 'Email',
                enabled: !_busy,
                keyboardType: TextInputType.emailAddress,
                validator: (value) {
                  final text = value?.trim() ?? '';
                  if (text.isEmpty) return 'Enter your email';
                  if (!text.contains('@')) return 'That does not look like an email address';
                  return null;
                },
              ),
              AuthTextField(
                controller: _password,
                label: 'Password',
                enabled: !_busy,
                obscure: _obscure,
                textInputAction: TextInputAction.done,
                onSubmitted: (_) => _busy ? null : _submit(),
                suffix: IconButton(
                  icon: Icon(_obscure ? Icons.visibility_outlined : Icons.visibility_off_outlined),
                  onPressed: () => setState(() => _obscure = !_obscure),
                ),
                validator: (value) =>
                    (value == null || value.isEmpty) ? 'Enter your password' : null,
              ),
              const SizedBox(height: 8),
              AuthSubmitButton(label: 'Sign in', busy: _busy, onPressed: _submit),
            ],
          ),
        ),
      ],
    );
  }
}
