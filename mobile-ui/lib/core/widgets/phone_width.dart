import 'package:flutter/material.dart';

// Caps the layout at phone width on a wide screen (e.g. desktop browser demo) — a no-op on an actual phone.
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
