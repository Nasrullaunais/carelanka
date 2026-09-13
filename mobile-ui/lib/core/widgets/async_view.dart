import 'package:flutter/material.dart';

import 'async_data.dart';

/// Renders the loading spinner, the error box with a retry, or the data.
class AsyncView<T> extends StatelessWidget {
  const AsyncView({
    super.key,
    required this.state,
    required this.builder,
    required this.onRetry,
  });

  final AsyncData<T> state;
  final Widget Function(BuildContext context, T value) builder;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return switch (state) {
      AsyncLoading<T>() => const Center(child: CircularProgressIndicator()),
      AsyncFailed<T>(:final error) => ErrorView(message: error.message, onRetry: onRetry),
      AsyncReady<T>(:final value) => builder(context, value),
    };
  }
}

/// Shows the server's own message. Never re-word it here — the API owns the
/// wording so it stays translatable.
class ErrorView extends StatelessWidget {
  const ErrorView({super.key, required this.message, required this.onRetry});

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(Icons.error_outline, size: 40, color: theme.colorScheme.error),
            const SizedBox(height: 12),
            Text(message, textAlign: TextAlign.center, style: theme.textTheme.bodyMedium),
            const SizedBox(height: 16),
            FilledButton.tonal(onPressed: onRetry, child: const Text('Try again')),
          ],
        ),
      ),
    );
  }
}

class EmptyView extends StatelessWidget {
  const EmptyView({super.key, required this.message, this.icon = Icons.inbox_outlined});

  final String message;
  final IconData icon;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(icon, size: 40, color: theme.colorScheme.outline),
            const SizedBox(height: 12),
            Text(message, textAlign: TextAlign.center, style: theme.textTheme.bodyMedium),
          ],
        ),
      ),
    );
  }
}
