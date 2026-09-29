import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../routing/app_router.dart';
import '../theme/app_theme.dart';

class WelcomeScreen extends StatelessWidget {
  const WelcomeScreen({super.key});

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
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Center(
                    child: Container(
                      width: 88,
                      height: 88,
                      decoration: BoxDecoration(
                        color: theme.colorScheme.primaryContainer,
                        borderRadius: BorderRadius.circular(AppTheme.radiusXL),
                      ),
                      child: Icon(
                        Icons.local_hospital_rounded,
                        size: 48,
                        color: theme.colorScheme.onPrimaryContainer,
                      ),
                    ),
                  ),
                  const SizedBox(height: 24),
                  Text(
                    'CareLanka',
                    textAlign: TextAlign.center,
                    style: theme.textTheme.displaySmall?.copyWith(fontWeight: FontWeight.w600),
                  ),
                  const SizedBox(height: 8),
                  Text(
                    'Book a visit, follow your stay, and read your\n'
                    'discharge instructions — from your phone.',
                    textAlign: TextAlign.center,
                    style: theme.textTheme.bodyLarge?.copyWith(
                      color: theme.colorScheme.onSurfaceVariant,
                    ),
                  ),
                  const SizedBox(height: 40),
                  FilledButton(
                    onPressed: () => context.go(AppRoutes.patientLogin),
                    child: const Text('I already have an account'),
                  ),
                  const SizedBox(height: 12),
                  OutlinedButton(
                    onPressed: () => context.go(AppRoutes.register),
                    child: const Text('Create an account'),
                  ),
                  const SizedBox(height: 32),
                  TextButton(
                    onPressed: () => context.go(AppRoutes.staffLogin),
                    child: const Text('Hospital staff sign in'),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}
