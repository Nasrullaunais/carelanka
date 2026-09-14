import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../routing/app_router.dart';
import 'auth_controller.dart';
import 'auth_form.dart';

class PatientLoginScreen extends StatefulWidget {
  const PatientLoginScreen({super.key});

  @override
  State<PatientLoginScreen> createState() => _PatientLoginScreenState();
}

class _PatientLoginScreenState extends State<PatientLoginScreen> {
  final _formKey = GlobalKey<FormState>();
  final _username = TextEditingController();
  final _password = TextEditingController();

  bool _busy = false;
  bool _obscure = true;

  @override
  void dispose() {
    _username.dispose();
    _password.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;

    setState(() => _busy = true);
    final auth = context.read<AuthController>();
    final signedIn = await auth.signInAsPatient(
      username: _username.text.trim(),
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
      title: 'Sign in',
      subtitle: 'Use the username you registered with.',
      children: [
        Form(
          key: _formKey,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              AuthTextField(
                controller: _username,
                label: 'Username',
                enabled: !_busy,
                keyboardType: TextInputType.text,
                // Deliberately only checked for emptiness. Anything more tells
                // a stranger what a valid username looks like.
                validator: (value) => (value == null || value.trim().isEmpty)
                    ? 'Enter your username'
                    : null,
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
        const SizedBox(height: 16),
        TextButton(
          onPressed: _busy ? null : () => context.go(AppRoutes.register),
          child: const Text('Create an account'),
        ),
      ],
    );
  }
}
