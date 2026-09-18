import 'package:flutter/material.dart';
import 'package:flutter_animate/flutter_animate.dart';

import '../theme/app_theme.dart';

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
    final scheme = theme.colorScheme;
    final mq = MediaQuery.of(context);

    return Scaffold(
      extendBodyBehindAppBar: true,
      appBar: showBack
          ? AppBar(
              backgroundColor: Colors.transparent,
              iconTheme: IconThemeData(color: scheme.onPrimary),
            )
          : null,
      body: SingleChildScrollView(
        child: Column(
          children: [
            // Gradient Hero Header
            Container(
              width: double.infinity,
              padding: EdgeInsets.fromLTRB(
                24,
                mq.padding.top + (showBack ? kToolbarHeight : 40),
                24,
                60, // Extra bottom padding to blend into the card overlap
              ),
              decoration: BoxDecoration(
                gradient: LinearGradient(
                  begin: Alignment.topLeft,
                  end: Alignment.bottomRight,
                  colors: [
                    scheme.primary,
                    scheme.primary.withValues(alpha: 0.8),
                  ],
                ),
                boxShadow: [
                  BoxShadow(
                    color: scheme.primary.withValues(alpha: 0.2),
                    blurRadius: 20,
                    offset: const Offset(0, 10),
                  ),
                ],
              ),
              child: SafeArea(
                bottom: false,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Container(
                      padding: const EdgeInsets.all(12),
                      decoration: BoxDecoration(
                        color: Colors.white.withValues(alpha: 0.2),
                        borderRadius: BorderRadius.circular(16),
                      ),
                      child: const Icon(Icons.favorite_rounded, color: Colors.white, size: 32),
                    ).animate().fadeIn(duration: 400.ms).slideY(begin: 0.2),
                    const SizedBox(height: 24),
                    Text(
                      title,
                      style: theme.textTheme.headlineMedium?.copyWith(
                        color: Colors.white,
                        fontWeight: FontWeight.w800,
                        letterSpacing: -0.5,
                      ),
                    ).animate().fadeIn(delay: 100.ms, duration: 400.ms).slideY(begin: 0.2),
                    if (subtitle != null) ...[
                      const SizedBox(height: 8),
                      Text(
                        subtitle!,
                        style: theme.textTheme.bodyMedium?.copyWith(
                          color: Colors.white.withValues(alpha: 0.9),
                          height: 1.5,
                        ),
                      ).animate().fadeIn(delay: 200.ms, duration: 400.ms).slideY(begin: 0.2),
                    ],
                  ],
                ),
              ),
            ),
            
            // Glassmorphic Form Card overlapping the header
            Transform.translate(
              offset: const Offset(0, -32),
              child: Padding(
                padding: const EdgeInsets.symmetric(horizontal: 24),
                child: Container(
                  constraints: const BoxConstraints(maxWidth: 420),
                  decoration: BoxDecoration(
                    color: theme.scaffoldBackgroundColor,
                    borderRadius: BorderRadius.circular(AppTheme.radiusL),
                    boxShadow: [
                      BoxShadow(
                        color: scheme.shadow.withValues(alpha: 0.1),
                        blurRadius: 30,
                        offset: const Offset(0, 10),
                      ),
                    ],
                  ),
                  padding: const EdgeInsets.all(28),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: children,
                  ),
                ).animate().fadeIn(delay: 300.ms, duration: 500.ms).slideY(begin: 0.1),
              ),
            ),
          ],
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

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;

    return Padding(
      padding: const EdgeInsets.only(bottom: 20),
      child: TextFormField(
        controller: controller,
        enabled: enabled,
        obscureText: obscure,
        keyboardType: keyboardType,
        textInputAction: textInputAction,
        onFieldSubmitted: onSubmitted,
        style: theme.textTheme.bodyLarge?.copyWith(fontWeight: FontWeight.w500),
        decoration: InputDecoration(
          labelText: label,
          labelStyle: TextStyle(color: scheme.onSurfaceVariant),
          floatingLabelStyle: TextStyle(
            color: scheme.primary,
            fontWeight: FontWeight.w600,
          ),
          filled: true,
          fillColor: scheme.surfaceContainerHighest.withValues(alpha: 0.3),
          border: OutlineInputBorder(
            borderRadius: BorderRadius.circular(AppTheme.radiusM),
            borderSide: BorderSide(color: scheme.outlineVariant.withValues(alpha: 0.5)),
          ),
          enabledBorder: OutlineInputBorder(
            borderRadius: BorderRadius.circular(AppTheme.radiusM),
            borderSide: BorderSide(color: scheme.outlineVariant.withValues(alpha: 0.5)),
          ),
          focusedBorder: OutlineInputBorder(
            borderRadius: BorderRadius.circular(AppTheme.radiusM),
            borderSide: BorderSide(color: scheme.primary, width: 2),
          ),
          errorBorder: OutlineInputBorder(
            borderRadius: BorderRadius.circular(AppTheme.radiusM),
            borderSide: BorderSide(color: scheme.error),
          ),
          contentPadding: const EdgeInsets.symmetric(horizontal: 20, vertical: 20),
          suffixIcon: suffix,
          errorText: (serverErrors?.isNotEmpty ?? false) ? serverErrors!.first : null,
        ),
        validator: validator,
      ),
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
      style: FilledButton.styleFrom(
        minimumSize: const Size.fromHeight(56),
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(AppTheme.radiusM),
        ),
      ),
      child: busy
          ? const SizedBox(
              height: 24, 
              width: 24, 
              child: CircularProgressIndicator(strokeWidth: 2.5, color: Colors.white)
            )
          : Text(
              label,
              style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w700, letterSpacing: 0.5),
            ),
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

String? validatePhoneNumber(String? value) {
  final text = value?.trim() ?? '';
  if (text.isEmpty) return 'Enter your phone number';

  final digits = text.replaceAll(RegExp(r'[\s-]'), '');
  if (!RegExp(r'^(\+94\d{9}|0\d{9})$').hasMatch(digits)) {
    return 'Use 07XXXXXXXX or +947XXXXXXXX';
  }
  return null;
}
