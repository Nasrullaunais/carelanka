import 'package:flutter/material.dart';

import '../theme/app_theme.dart';
import 'async_data.dart';

/// Renders the loading state, the error box with a retry, or the data.
class AsyncView<T> extends StatelessWidget {
  const AsyncView({
    super.key,
    required this.state,
    required this.builder,
    required this.onRetry,
    this.loading,
  });

  final AsyncData<T> state;
  final Widget Function(BuildContext context, T value) builder;
  final VoidCallback onRetry;

  /// A skeleton shaped like the screen underneath. A spinner in the middle of
  /// an empty page tells the reader nothing about what is coming.
  final Widget? loading;

  @override
  Widget build(BuildContext context) {
    return switch (state) {
      AsyncLoading<T>() => loading ?? const Center(child: CircularProgressIndicator()),
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
        padding: const EdgeInsets.all(AppTheme.gutter),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Container(
              padding: const EdgeInsets.all(16),
              decoration: BoxDecoration(
                color: theme.colorScheme.errorContainer,
                shape: BoxShape.circle,
              ),
              child: Icon(
                Icons.cloud_off_rounded,
                size: 28,
                color: theme.colorScheme.onErrorContainer,
              ),
            ),
            const SizedBox(height: 16),
            Text(
              'Something went wrong',
              style: theme.textTheme.titleMedium,
              textAlign: TextAlign.center,
            ),
            const SizedBox(height: 6),
            Text(
              message,
              textAlign: TextAlign.center,
              style: theme.textTheme.bodyMedium
                  ?.copyWith(color: theme.colorScheme.onSurfaceVariant),
            ),
            const SizedBox(height: 20),
            FilledButton.tonalIcon(
              onPressed: onRetry,
              icon: const Icon(Icons.refresh),
              label: const Text('Try again'),
              style: FilledButton.styleFrom(minimumSize: const Size(160, 48)),
            ),
          ],
        ),
      ),
    );
  }
}

/// Nothing to show yet. [action] is what the reader can do about it — an empty
/// screen with no way forward is a dead end.
class EmptyView extends StatelessWidget {
  const EmptyView({
    super.key,
    required this.message,
    this.title,
    this.icon = Icons.inbox_outlined,
    this.action,
  });

  final String message;
  final String? title;
  final IconData icon;
  final Widget? action;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Center(
      child: Padding(
        padding: const EdgeInsets.all(AppTheme.gutter),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Container(
              padding: const EdgeInsets.all(18),
              decoration: BoxDecoration(
                color: theme.colorScheme.surfaceContainerHighest,
                shape: BoxShape.circle,
              ),
              child: Icon(icon, size: 30, color: theme.colorScheme.onSurfaceVariant),
            ),
            const SizedBox(height: 16),
            if (title != null) ...[
              Text(title!, style: theme.textTheme.titleMedium, textAlign: TextAlign.center),
              const SizedBox(height: 6),
            ],
            Text(
              message,
              textAlign: TextAlign.center,
              style: theme.textTheme.bodyMedium
                  ?.copyWith(color: theme.colorScheme.onSurfaceVariant),
            ),
            if (action != null) ...[
              const SizedBox(height: 20),
              action!,
            ],
          ],
        ),
      ),
    );
  }
}

/// A grey block standing in for content that has not arrived, pulsing so it
/// reads as "loading" rather than "broken".
class Skeleton extends StatefulWidget {
  const Skeleton({super.key, required this.height, this.width, this.radius = AppTheme.radiusS});

  const Skeleton.card({super.key, this.height = 120, this.width, this.radius = AppTheme.radiusL});

  final double height;
  final double? width;
  final double radius;

  @override
  State<Skeleton> createState() => _SkeletonState();
}

class _SkeletonState extends State<Skeleton> with SingleTickerProviderStateMixin {
  late final AnimationController _pulse = AnimationController(
    vsync: this,
    duration: const Duration(milliseconds: 1100),
  )..repeat(reverse: true);

  @override
  void dispose() {
    _pulse.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final base = Theme.of(context).colorScheme.surfaceContainerHighest;

    return FadeTransition(
      opacity: Tween<double>(begin: 0.45, end: 1).animate(_pulse),
      child: Container(
        height: widget.height,
        width: widget.width,
        decoration: BoxDecoration(
          color: base,
          borderRadius: BorderRadius.circular(widget.radius),
        ),
      ),
    );
  }
}
