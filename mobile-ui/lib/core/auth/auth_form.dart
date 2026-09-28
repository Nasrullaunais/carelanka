import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

class AuthScaffold extends StatelessWidget {
  const AuthScaffold({
    super.key,
    required this.title,
    required this.children,
    this.subtitle,
    this.showBack = true,
  });

  final String title;
  final String? subtitle;
  final List<Widget> children;
  final bool showBack;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Scaffold(
      appBar: showBack ? AppBar(backgroundColor: Colors.transparent) : null,
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.fromLTRB(24, 8, 24, 32),
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 420),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Text(title, style: theme.textTheme.headlineMedium),
                  if (subtitle != null) ...[
                    const SizedBox(height: 8),
                    Text(
                      subtitle!,
                      style: theme.textTheme.bodyMedium?.copyWith(
                        color: theme.colorScheme.onSurfaceVariant,
                      ),
                    ),
                  ],
                  const SizedBox(height: 28),
                  ...children,
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class AuthTextField extends StatelessWidget {
  const AuthTextField({
    super.key,
    required this.controller,
    required this.label,
    required this.enabled,
    this.validator,
    this.keyboardType,
    this.obscure = false,
    this.textInputAction = TextInputAction.next,
    this.onSubmitted,
    this.suffix,
    this.serverErrors,
    this.inputFormatters,
    this.focusNode,
  });

  final TextEditingController controller;
  final String label;
  final bool enabled;
  final String? Function(String?)? validator;
  final TextInputType? keyboardType;
  final bool obscure;
  final TextInputAction textInputAction;
  final void Function(String)? onSubmitted;
  final Widget? suffix;
  final List<String>? serverErrors;
  final List<TextInputFormatter>? inputFormatters;
  final FocusNode? focusNode;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 16),
      child: TextFormField(
        controller: controller,
        focusNode: focusNode,
        enabled: enabled,
        obscureText: obscure,
        keyboardType: keyboardType,
        textInputAction: textInputAction,
        onFieldSubmitted: onSubmitted,
        inputFormatters: inputFormatters,
        decoration: InputDecoration(
          labelText: label,
          border: const OutlineInputBorder(),
          suffixIcon: suffix,
          errorText: (serverErrors?.isNotEmpty ?? false) ? serverErrors!.first : null,
        ),
        validator: validator,
      ),
    );
  }
}

/// Each password box owns its own show/hide button, visible only while that box is
/// being typed in. Leaving the box hides the text again.
class AuthPasswordField extends StatefulWidget {
  const AuthPasswordField({
    super.key,
    required this.controller,
    required this.label,
    required this.enabled,
    this.validator,
    this.textInputAction = TextInputAction.next,
    this.onSubmitted,
    this.serverErrors,
  });

  final TextEditingController controller;
  final String label;
  final bool enabled;
  final String? Function(String?)? validator;
  final TextInputAction textInputAction;
  final void Function(String)? onSubmitted;
  final List<String>? serverErrors;

  @override
  State<AuthPasswordField> createState() => _AuthPasswordFieldState();
}

class _AuthPasswordFieldState extends State<AuthPasswordField> {
  final _focus = FocusNode();
  bool _obscure = true;

  @override
  void initState() {
    super.initState();
    _focus.addListener(_onFocusChange);
  }

  @override
  void dispose() {
    _focus
      ..removeListener(_onFocusChange)
      ..dispose();
    super.dispose();
  }

  void _onFocusChange() {
    setState(() {
      if (!_focus.hasFocus) _obscure = true;
    });
  }

  @override
  Widget build(BuildContext context) {
    return AuthTextField(
      controller: widget.controller,
      focusNode: _focus,
      label: widget.label,
      enabled: widget.enabled,
      obscure: _obscure,
      textInputAction: widget.textInputAction,
      onSubmitted: widget.onSubmitted,
      serverErrors: widget.serverErrors,
      validator: widget.validator,
      suffix: _focus.hasFocus
          ? IconButton(
              tooltip: _obscure ? 'Show password' : 'Hide password',
              icon: Icon(_obscure ? Icons.visibility_outlined : Icons.visibility_off_outlined),
              onPressed: () => setState(() => _obscure = !_obscure),
            )
          : null,
    );
  }
}

class AuthSubmitButton extends StatelessWidget {
  const AuthSubmitButton({
    super.key,
    required this.label,
    required this.busy,
    required this.onPressed,
  });

  final String label;
  final bool busy;
  final VoidCallback onPressed;

  @override
  Widget build(BuildContext context) {
    return FilledButton(
      onPressed: busy ? null : onPressed,
      style: FilledButton.styleFrom(minimumSize: const Size.fromHeight(48)),
      child: busy
          ? const SizedBox(height: 20, width: 20, child: CircularProgressIndicator(strokeWidth: 2))
          : Text(label),
    );
  }
}

// Deliberately not shown on the sign-in screen — it would tell a stranger what a valid username looks like.
String? validateUsername(String? value) {
  final text = value?.trim() ?? '';
  if (text.isEmpty) return 'Choose a username';
  if (text.length < 3) return 'Use at least 3 characters';
  if (text.length > 50) return 'Use 50 characters or fewer';
  if (!RegExp(r'^[a-zA-Z0-9._-]+$').hasMatch(text)) {
    return 'Letters, numbers, dots, underscores and hyphens only';
  }
  return null;
}

String? validatePassword(String? value) {
  final text = value ?? '';
  if (text.length < 8) return 'Use at least 8 characters';
  if (text.length > 20) return 'Use 20 characters or fewer';
  if (!RegExp(r'[A-Z]').hasMatch(text)) return 'Include at least one capital letter';
  return null;
}

String? validatePhoneNumber(String? value) {
  final text = value?.trim() ?? '';
  if (text.isEmpty) return 'Enter your phone number';

  final digits = text.replaceAll(RegExp(r'[\s-]'), '');
  if (!RegExp(r'^(\+94\d{9}|0\d{9})$').hasMatch(digits)) {
    return 'Use 07XXXXXXXX or +947XXXXXXXX';
  }
  return null;
}
