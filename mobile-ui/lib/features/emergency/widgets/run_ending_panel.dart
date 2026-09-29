import 'package:flutter/material.dart';

import '../../../core/theme/app_theme.dart';
import '../../../core/widgets/notice_banner.dart';
import '../models/run_ending.dart';

class RunEndingPanel extends StatelessWidget {
  const RunEndingPanel({
    super.key,
    required this.ending,
    required this.onDismiss,
  });

  final RunEnding ending;
  final VoidCallback onDismiss;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final (icon, accent) = switch (ending.kind) {
      RunEndingKind.cancelled => (Icons.cancel_outlined, scheme.error),
      RunEndingKind.reassigned => (Icons.swap_horiz, scheme.warning),
      RunEndingKind.diverted => (Icons.alt_route, scheme.warning),
      RunEndingKind.handedOver || RunEndingKind.endedAtScene => (
        Icons.check_circle_outline,
        scheme.primary,
      ),
      RunEndingKind.unavailable => (Icons.info_outline, scheme.warning),
    };

    return Padding(
      padding: const EdgeInsets.fromLTRB(
        AppTheme.gutter,
        AppTheme.gutter,
        AppTheme.gutter,
        0,
      ),
      child: Semantics(
        liveRegion: true,
        child: NoticeBanner(
          icon: icon,
          accent: accent,
          title: ending.title,
          body: ending.message,
          action: OutlinedButton(
            onPressed: onDismiss,
            style: OutlinedButton.styleFrom(minimumSize: const Size(96, 44)),
            child: const Text('OK'),
          ),
        ),
      ),
    );
  }
}
