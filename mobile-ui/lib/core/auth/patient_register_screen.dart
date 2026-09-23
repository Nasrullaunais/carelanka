import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../routing/app_router.dart';
import 'auth_controller.dart';
import 'auth_form.dart';

class PatientRegisterScreen extends StatefulWidget {
  const PatientRegisterScreen({super.key});

  @override
  State<PatientRegisterScreen> createState() => _PatientRegisterScreenState();
}

class _PatientRegisterScreenState extends State<PatientRegisterScreen> {
  final _formKey = GlobalKey<FormState>();
  final _username = TextEditingController();
  final _password = TextEditingController();
  final _confirm = TextEditingController();

  bool _busy = false;
  bool _obscure = true;

  @override
  void dispose() {
    for (final controller in [_username, _password, _confirm]) {
      controller.dispose();
    }
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;

    setState(() => _busy = true);
    final auth = context.read<AuthController>();
    final created = await auth.registerAsPatient(
      username: _username.text.trim(),
      password: _password.text,
    );

    if (!mounted) return;
    setState(() => _busy = false);

    if (!created) {
      final message = auth.lastError?.message ?? 'Could not create your account.';
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(message)));
    }
  }

  @override
  Widget build(BuildContext context) {
    final fieldErrors = context.watch<AuthController>().lastError?.fieldErrors ?? const {};

    return AuthScaffold(
      title: 'Create an account',
      subtitle: 'Pick a username and a password. Your name and the rest of '
          'your details come next.',
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
                serverErrors: fieldErrors['username'],
                inputFormatters: [FilteringTextInputFormatter.deny(RegExp(r'\s'))],
                validator: validateUsername,
              ),
              AuthTextField(
                controller: _password,
                label: 'Password',
                enabled: !_busy,
                obscure: _obscure,
                serverErrors: fieldErrors['password'],
                suffix: IconButton(
                  icon: Icon(_obscure ? Icons.visibility_outlined : Icons.visibility_off_outlined),
                  onPressed: () => setState(() => _obscure = !_obscure),
                ),
                validator: validatePassword,
              ),
              AuthTextField(
                controller: _confirm,
                label: 'Confirm password',
                enabled: !_busy,
                obscure: _obscure,
                textInputAction: TextInputAction.done,
                onSubmitted: (_) => _busy ? null : _submit(),
                validator: (value) =>
                    (value != _password.text) ? 'Passwords do not match' : null,
              ),
              const SizedBox(height: 8),
              AuthSubmitButton(label: 'Create account', busy: _busy, onPressed: _submit),
            ],
          ),
        ),
        const SizedBox(height: 16),
        TextButton(
          onPressed: _busy ? null : () => context.go(AppRoutes.patientLogin),
          child: const Text('I already have an account'),
        ),
      ],
    );
  }
}
