import 'dart:async';

import 'package:flutter/material.dart';

import '../../../core/utils/friendly_date.dart';

/// "Sent 3 minutes ago", redrawn exactly when the next minute is reached.
class SentAgo extends StatefulWidget {
  const SentAgo({super.key, required this.sentAt, this.now = DateTime.now});

  final DateTime sentAt;
  final DateTime Function() now;

  @override
  State<SentAgo> createState() => _SentAgoState();
}

class _SentAgoState extends State<SentAgo> {
  Timer? _tick;

  @override
  void initState() {
    super.initState();
    _scheduleTick();
  }

  @override
  void didUpdateWidget(SentAgo oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.sentAt != widget.sentAt) _scheduleTick();
  }

  @override
  void dispose() {
    _tick?.cancel();
    super.dispose();
  }

  Duration get _elapsed {
    final elapsed = widget.now().difference(widget.sentAt);
    return elapsed.isNegative ? Duration.zero : elapsed;
  }

  void _scheduleTick() {
    _tick?.cancel();
    final elapsed = _elapsed;
    final untilNextMinute = Duration(minutes: elapsed.inMinutes + 1) - elapsed;
    _tick = Timer(untilNextMinute, () {
      setState(() {});
      _scheduleTick();
    });
  }

  @override
  Widget build(BuildContext context) => Text(
    _elapsed < const Duration(minutes: 1)
        ? 'Sent just now'
        : 'Sent ${FriendlyDate.countdown(widget.sentAt, now: widget.now())}',
    style: Theme.of(context).textTheme.bodyLarge?.copyWith(
      color: Theme.of(context).colorScheme.onSurfaceVariant,
    ),
  );
}
