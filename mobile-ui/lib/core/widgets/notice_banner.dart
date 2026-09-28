import 'package:flutter/material.dart';

import '../theme/app_theme.dart';

class NoticeBanner extends StatelessWidget {
  const NoticeBanner({
    super.key,
    required this.title,
    required this.accent,
    required this.icon,
    this.body,
    this.bullets = const [],
    this.action,
  });

  final String title;
  final String? body;
  final List<String> bullets;

  final Color accent;
  final IconData icon;
  final Widget? action;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;

    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: Color.alphaBlend(accent.withValues(alpha: 0.07), scheme.surface),
        borderRadius: BorderRadius.circular(AppTheme.radiusL),
        border: Border.all(color: accent.withValues(alpha: 0.28)),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Container(
            width: 36,
            height: 36,
            decoration: BoxDecoration(
              color: accent.withValues(alpha: 0.14),
              shape: BoxShape.circle,
            ),
            child: Icon(icon, size: 19, color: accent),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Padding(
                  padding: const EdgeInsets.only(top: 7),
                  child: Text(title, style: theme.textTheme.titleSmall),
                ),
                if (body != null) ...[
                  const SizedBox(height: 4),
                  Text(
                    body!,
                    style: theme.textTheme.bodyMedium?.copyWith(
                      color: scheme.onSurfaceVariant,
                    ),
                  ),
                ],
                if (bullets.isNotEmpty) ...[
                  const SizedBox(height: 10),
                  Wrap(
                    spacing: 6,
                    runSpacing: 6,
                    children: [
                      for (final bullet in bullets)
                        Container(
                          padding: const EdgeInsets.symmetric(
                            horizontal: 10,
                            vertical: 5,
                          ),
                          decoration: BoxDecoration(
                            color: scheme.surface,
                            borderRadius: BorderRadius.circular(
                              AppTheme.radiusS,
                            ),
                            border: Border.all(color: scheme.outlineVariant),
                          ),
                          child: Text(
                            bullet,
                            style: theme.textTheme.labelMedium?.copyWith(
                              color: scheme.onSurface,
                            ),
                          ),
                        ),
                    ],
                  ),
                ],
                if (action != null) ...[
                  const SizedBox(height: 14),
                  Align(alignment: Alignment.centerLeft, child: action!),
                ],
              ],
            ),
          ),
        ],
      ),
    );
  }
}
