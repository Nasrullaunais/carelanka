import 'package:flutter/material.dart';

/// Keeps a phone layout at phone width on a big screen.
///
/// This app is designed for a hand. Run it in a desktop browser — which is how
/// it gets demonstrated — and a row of cards stretches to 1500px, the line
/// length becomes unreadable and a two-column grid turns into two billboards.
/// On an actual phone the constraint never binds and this does nothing.
class PhoneWidth extends StatelessWidget {
  const PhoneWidth({super.key, required this.child, this.maxWidth = 480});

  final Widget child;
  final double maxWidth;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;

    if (MediaQuery.sizeOf(context).width <= maxWidth) return child;

    return ColoredBox(
      color: scheme.surfaceContainerHighest,
      child: Center(
        child: ConstrainedBox(
          constraints: BoxConstraints(maxWidth: maxWidth),
          child: child,
        ),
      ),
    );
  }
}
