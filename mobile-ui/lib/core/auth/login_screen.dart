import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import 'auth_controller.dart';

enum _LoginMode { staff, patient }

class LoginScreen extends StatefulWidget {
  const LoginScreen({super.key});

  @override
  State<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginScreen> {
  final _formKey = GlobalKey<FormState>();
  final _identifier = TextEditingController();
  final _password = TextEditingController();

  _LoginMode _mode = _LoginMode.staff;
  bool _submitting = false;
  bool _obscure = true;

  @override
  void dispose() {
    _identifier.dispose();
    _password.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;

    setState(() => _submitting = true);
    final auth = context.read<AuthController>();
    final ok = _mode == _LoginMode.staff
        ? await auth.signInAsStaff(email: _identifier.text.trim(), password: _password.text)
        : await auth.signInAsPatient(phoneNumber: _identifier.text.trim(), password: _password.text);

    if (!mounted) return;
    setState(() => _submitting = false);

    if (!ok) {
      final message = auth.lastError?.message ?? 'Could not sign in.';
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(message)));
    }
  }

  String? _validateIdentifier(String? value) {
    final text = value?.trim() ?? '';
    if (text.isEmpty) {
      return _mode == _LoginMode.staff ? 'Enter your email' : 'Enter your phone number';
    }
    if (_mode == _LoginMode.staff && !text.contains('@')) {
      return 'That does not look like an email address';
    }
    return null;
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Scaffold(
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(24),
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 420),
              child: Form(
                key: _formKey,
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Text('CareLanka', style: theme.textTheme.headlineMedium, textAlign: TextAlign.center),
                    const SizedBox(height: 24),
                    SegmentedButton<_LoginMode>(
                      segments: const [
                        ButtonSegment(value: _LoginMode.staff, label: Text('Staff')),
                        ButtonSegment(value: _LoginMode.patient, label: Text('Patient')),
                      ],
                      selected: {_mode},
                      onSelectionChanged: _submitting
                          ? null
                          : (selection) => setState(() {
                                _mode = selection.first;
                                _formKey.currentState?.reset();
                              }),
                    ),
                    const SizedBox(height: 24),
                    TextFormField(
                      controller: _identifier,
                      enabled: !_submitting,
                      keyboardType: _mode == _LoginMode.staff
                          ? TextInputType.emailAddress
                          : TextInputType.phone,
                      textInputAction: TextInputAction.next,
                      decoration: InputDecoration(
                        labelText: _mode == _LoginMode.staff ? 'Email' : 'Phone number',
                        border: const OutlineInputBorder(),
                      ),
                      validator: _validateIdentifier,
                    ),
                    const SizedBox(height: 16),
                    TextFormField(
                      controller: _password,
                      enabled: !_submitting,
                      obscureText: _obscure,
                      textInputAction: TextInputAction.done,
                      onFieldSubmitted: (_) => _submitting ? null : _submit(),
                      decoration: InputDecoration(
                        labelText: 'Password',
                        border: const OutlineInputBorder(),
                        suffixIcon: IconButton(
                          icon: Icon(_obscure ? Icons.visibility_outlined : Icons.visibility_off_outlined),
                          onPressed: () => setState(() => _obscure = !_obscure),
                        ),
                      ),
                      validator: (value) =>
                          (value == null || value.isEmpty) ? 'Enter your password' : null,
                    ),
                    const SizedBox(height: 24),
                    FilledButton(
                      onPressed: _submitting ? null : _submit,
                      child: _submitting
                          ? const SizedBox(
                              height: 20,
                              width: 20,
                              child: CircularProgressIndicator(strokeWidth: 2),
                            )
                          : const Text('Sign in'),
                    ),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
