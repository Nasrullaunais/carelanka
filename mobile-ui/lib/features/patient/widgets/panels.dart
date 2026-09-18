import 'package:flutter/material.dart';

import '../../../core/theme/app_theme.dart';

class SectionCard extends StatelessWidget {
  const SectionCard({
    super.key,
    required this.child,
    this.title,
    this.icon,
    this.trailing,
    this.padding = const EdgeInsets.all(20),
  });

  final Widget child;
  final String? title;
  final IconData? icon;
  final Widget? trailing;
  final EdgeInsets padding;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;

    return Container(
      decoration: BoxDecoration(
        color: scheme.surface,
        borderRadius: BorderRadius.circular(AppTheme.radiusL),
        border: Border.all(color: scheme.outlineVariant.withValues(alpha: 0.15)),
        boxShadow: [
          BoxShadow(
            color: scheme.shadow.withValues(alpha: 0.06),
            blurRadius: 16,
            offset: const Offset(0, 4),
          ),
        ],
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          if (title != null) ...[
            Padding(
              padding: EdgeInsets.fromLTRB(padding.left, padding.top, padding.right, 0),
              child: Row(
                children: [
                  if (icon != null) ...[
                    Container(
                      padding: const EdgeInsets.all(8),
                      decoration: BoxDecoration(
                        color: scheme.primaryContainer.withValues(alpha: 0.5),
                        shape: BoxShape.circle,
                      ),
                      child: Icon(icon, size: 16, color: scheme.primary),
                    ),
                    const SizedBox(width: 10),
                  ],
                  Expanded(
                    child: Text(
                      title!,
                      style: theme.textTheme.titleSmall?.copyWith(
                        fontWeight: FontWeight.w700,
                        letterSpacing: 0.2,
                      ),
                    ),
                  ),
                  if (trailing != null) trailing!,
                ],
              ),
            ),
            Padding(
              padding: EdgeInsets.symmetric(horizontal: padding.left),
              child: Divider(
                height: 24,
                color: scheme.outlineVariant.withValues(alpha: 0.2),
              ),
            ),
          ],
          Padding(
            padding: EdgeInsets.fromLTRB(
              padding.left,
              title != null ? 0 : padding.top,
              padding.right,
              padding.bottom,
            ),
            child: child,
          ),
        ],
      ),
    );
  }
}

class DetailRow extends StatelessWidget {
  const DetailRow({super.key, required this.label, required this.value, this.icon});

  final String label;
  final String? value;
  final IconData? icon;

  @override
  Widget build(BuildContext context) {
    if (value == null) return const SizedBox.shrink();

    final theme = Theme.of(context);
    final scheme = theme.colorScheme;

    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 8),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          if (icon != null) ...[
            Container(
              padding: const EdgeInsets.all(6),
              decoration: BoxDecoration(
                color: scheme.surfaceContainerHighest.withValues(alpha: 0.7),
                shape: BoxShape.circle,
              ),
              child: Icon(icon, size: 14, color: scheme.onSurfaceVariant),
            ),
            const SizedBox(width: 12),
          ],
          SizedBox(
            width: 100,
            child: Padding(
              padding: const EdgeInsets.only(top: 4),
              child: Text(
                label,
                style: theme.textTheme.bodySmall?.copyWith(
                  color: scheme.onSurfaceVariant,
                  fontWeight: FontWeight.w500,
                ),
              ),
            ),
          ),
          const SizedBox(width: 8),
          Expanded(
            child: Padding(
              padding: const EdgeInsets.only(top: 4),
              child: Text(
                value!,
                style: theme.textTheme.bodyMedium?.copyWith(
                  fontWeight: FontWeight.w600,
                  color: scheme.onSurface,
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

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
      decoration: BoxDecoration(
        color: scheme.surface,
        borderRadius: BorderRadius.circular(AppTheme.radiusL),
        border: Border.all(color: scheme.outlineVariant.withValues(alpha: 0.15)),
        boxShadow: [
          BoxShadow(
            color: accent.withValues(alpha: 0.06),
            blurRadius: 12,
            offset: const Offset(0, 4),
          ),
        ],
      ),
      // IntrinsicHeight: stretch alone can't size the accent edge to match unbounded content in a list.
      child: IntrinsicHeight(
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Container(
              width: 6,
              decoration: BoxDecoration(
                color: accent,
                borderRadius: const BorderRadius.horizontal(
                  left: Radius.circular(AppTheme.radiusL),
                ),
              ),
            ),
            Expanded(
              child: Padding(
                padding: const EdgeInsets.fromLTRB(16, 18, 18, 18),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Container(
                          padding: const EdgeInsets.all(6),
                          decoration: BoxDecoration(
                            color: accent.withValues(alpha: 0.1),
                            shape: BoxShape.circle,
                          ),
                          child: Icon(icon, size: 16, color: accent),
                        ),
                        const SizedBox(width: 10),
                        Expanded(
                          child: Padding(
                            padding: const EdgeInsets.only(top: 4),
                            child: Text(
                              title,
                              style: theme.textTheme.titleSmall?.copyWith(
                                fontWeight: FontWeight.w700,
                              ),
                            ),
                          ),
                        ),
                      ],
                    ),
                    if (body != null) ...[
                      const SizedBox(height: 8),
                      Padding(
                        padding: const EdgeInsets.only(left: 32),
                        child: Text(
                          body!,
                          style: theme.textTheme.bodySmall?.copyWith(
                            color: scheme.onSurfaceVariant,
                            height: 1.5,
                          ),
                        ),
                      ),
                    ],
                    if (bullets.isNotEmpty) ...[
                      const SizedBox(height: 12),
                      Padding(
                        padding: const EdgeInsets.only(left: 32),
                        child: Wrap(
                          spacing: 6,
                          runSpacing: 6,
                          children: [
                            for (final bullet in bullets)
                              Container(
                                padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
                                decoration: BoxDecoration(
                                  color: scheme.surfaceContainerHighest.withValues(alpha: 0.8),
                                  borderRadius: BorderRadius.circular(AppTheme.radiusS),
                                ),
                                child: Text(
                                  bullet,
                                  style: theme.textTheme.labelSmall?.copyWith(
                                    color: scheme.onSurfaceVariant,
                                    fontWeight: FontWeight.w600,
                                  ),
                                ),
                              ),
                          ],
                        ),
                      ),
                    ],
                    if (action != null) ...[
                      const SizedBox(height: 14),
                      Padding(
                        padding: const EdgeInsets.only(left: 32),
                        child: Align(alignment: Alignment.centerLeft, child: action!),
                      ),
                    ],
                  ],
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class DateBlock extends StatelessWidget {
  const DateBlock({super.key, required this.date, required this.muted});

  final DateTime date;
  final bool muted;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;
    final local = date.toLocal();
    const months = [
      'JAN', 'FEB', 'MAR', 'APR', 'MAY', 'JUN',
      'JUL', 'AUG', 'SEP', 'OCT', 'NOV', 'DEC',
    ];

    return Container(
      width: 58,
      padding: const EdgeInsets.symmetric(vertical: 10),
      decoration: BoxDecoration(
        gradient: muted
            ? null
            : LinearGradient(
                begin: Alignment.topLeft,
                end: Alignment.bottomRight,
                colors: [
                  scheme.primaryContainer,
                  scheme.primaryContainer.withValues(alpha: 0.6),
                ],
              ),
        color: muted ? scheme.surfaceContainerHighest : null,
        borderRadius: BorderRadius.circular(AppTheme.radiusM),
        boxShadow: muted
            ? null
            : [
                BoxShadow(
                  color: scheme.primary.withValues(alpha: 0.1),
                  blurRadius: 8,
                  offset: const Offset(0, 2),
                ),
              ],
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(
            months[local.month - 1],
            style: theme.textTheme.labelSmall?.copyWith(
              color: muted ? scheme.onSurfaceVariant : scheme.onPrimaryContainer,
              fontWeight: FontWeight.w800,
              letterSpacing: 1,
            ),
          ),
          Text(
            '${local.day}',
            style: theme.textTheme.headlineSmall?.copyWith(
              color: muted ? scheme.onSurfaceVariant : scheme.onPrimaryContainer,
              fontWeight: FontWeight.w800,
              height: 1.1,
            ),
          ),
        ],
      ),
    );
  }
}
