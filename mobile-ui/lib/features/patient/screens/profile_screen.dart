import 'package:flutter/material.dart';
import 'package:flutter_animate/flutter_animate.dart';
import 'package:provider/provider.dart';

import '../../../core/auth/auth_controller.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/utils/friendly_date.dart';
import '../../../services/api_client/models/my_profile.dart';
import '../state/profile_controller.dart';
import '../widgets/dialer.dart';
import '../widgets/panels.dart';
import 'claim_record_screen.dart';
import 'my_details_screen.dart';
import 'my_reports_screen.dart';
import 'past_visits_screen.dart';

class ProfileScreen extends StatelessWidget {
  const ProfileScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final profileController = context.watch<ProfileController>();
    final profile = profileController.profile.valueOrNull;
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;

    return Scaffold(
      body: CustomScrollView(
        slivers: [
          SliverToBoxAdapter(
            child: SafeArea(
              bottom: false,
              child: Padding(
                padding: const EdgeInsets.fromLTRB(AppTheme.gutter, 24, AppTheme.gutter, 16),
                child: Text(
                  'Profile',
                  style: theme.textTheme.headlineMedium?.copyWith(fontWeight: FontWeight.w800),
                ).animate().fadeIn().slideY(begin: -0.2),
              ),
            ),
          ),
          SliverFillRemaining(
            hasScrollBody: false,
            child: ListView(
              padding: const EdgeInsets.fromLTRB(AppTheme.gutter, 4, AppTheme.gutter, 104),
              physics: const NeverScrollableScrollPhysics(),
              shrinkWrap: true,
              children: [
                if (profile == null) ...[
                  const _AccountOnlyHeader().animate().fadeIn(duration: 400.ms).slideY(begin: 0.1),
                  const SizedBox(height: 24),
                  NoticeBanner(
                    icon: Icons.badge_outlined,
                    accent: scheme.warning,
                    title: 'No hospital record',
                    body: 'Add your details to create your hospital record. If the hospital has '
                        'already registered you at the desk, use your patient code instead so '
                        'your stay and history come with you.',
                    action: Wrap(
                      spacing: 12,
                      runSpacing: 12,
                      children: [
                        FilledButton(
                          onPressed: () => openMyDetails(context, profileController),
                          style: FilledButton.styleFrom(minimumSize: const Size(0, 48)),
                          child: const Text('Add my details'),
                        ),
                        OutlinedButton(
                          onPressed: () => openClaimRecord(context, profileController),
                          style: OutlinedButton.styleFrom(minimumSize: const Size(0, 48)),
                          child: const Text('I have a patient code'),
                        ),
                      ],
                    ),
                  ).animate().fadeIn(delay: 100.ms).slideY(begin: 0.1),
                  const SizedBox(height: 20),
                ] else ...[
                  _Header(profile: profile).animate().fadeIn(duration: 400.ms).slideY(begin: 0.1),
                  const SizedBox(height: 24),
                  if (!profile.detailsComplete) ...[
                    NoticeBanner(
                      icon: Icons.info_outline,
                      accent: scheme.warning,
                      title: 'Incomplete details',
                      bullets: profile.missingFields.map(prettyFieldName).toList(),
                      action: OutlinedButton(
                        onPressed: () => openMyDetails(context, profileController),
                        style: OutlinedButton.styleFrom(
                          minimumSize: const Size(0, 42),
                          padding: const EdgeInsets.symmetric(horizontal: 20),
                        ),
                        child: const Text('Add them now'),
                      ),
                    ).animate().fadeIn(delay: 50.ms).slideY(begin: 0.1),
                    const SizedBox(height: 20),
                  ],
                  SectionCard(
                    title: 'Your details',
                    icon: Icons.badge_outlined,
                    trailing: TextButton(
                      onPressed: () => openMyDetails(context, profileController),
                      child: const Text('Edit', style: TextStyle(fontWeight: FontWeight.w700)),
                    ),
                    child: Column(
                      children: [
                        DetailRow(label: 'NIC', value: profile.nic, icon: Icons.pin_outlined),
                        DetailRow(
                          label: 'Gender',
                          value: genderLabel(profile.gender),
                          icon: Icons.wc_outlined,
                        ),
                        DetailRow(
                          label: 'Born',
                          value: profile.dateOfBirth == null
                              ? null
                              : FriendlyDate.date(profile.dateOfBirth!),
                          icon: Icons.cake_outlined,
                        ),
                        DetailRow(label: 'Phone', value: profile.phone, icon: Icons.phone_outlined),
                        DetailRow(label: 'Address', value: profile.address, icon: Icons.home_outlined),
                      ],
                    ),
                  ).animate().fadeIn(delay: 100.ms).slideY(begin: 0.1),
                  const SizedBox(height: 20),
                  _EmergencyContact(profile: profile).animate().fadeIn(delay: 200.ms).slideY(begin: 0.1),
                  const SizedBox(height: 20),
                ],
                Container(
                  decoration: BoxDecoration(
                    color: scheme.surface,
                    borderRadius: BorderRadius.circular(AppTheme.radiusL),
                    border: Border.all(color: scheme.outlineVariant.withValues(alpha: 0.15)),
                    boxShadow: [
                      BoxShadow(
                        color: scheme.shadow.withValues(alpha: 0.05),
                        blurRadius: 10,
                        offset: const Offset(0, 4),
                      ),
                    ],
                  ),
                  child: Column(
                    children: [
                      _MenuTile(
                        icon: Icons.history_rounded,
                        title: 'Past visits',
                        subtitle: 'Completed stays',
                        onTap: () => Navigator.of(context).push(
                          MaterialPageRoute(builder: (_) => const PastVisitsScreen()),
                        ),
                      ),
                      Divider(indent: 64, endIndent: 20, color: scheme.outlineVariant.withValues(alpha: 0.2)),
                      _MenuTile(
                        icon: Icons.description_outlined,
                        title: 'My reports',
                        subtitle: 'Lab results',
                        onTap: () => Navigator.of(context).push(
                          MaterialPageRoute(builder: (_) => const MyReportsScreen()),
                        ),
                      ),
                      Divider(indent: 64, endIndent: 20, color: scheme.outlineVariant.withValues(alpha: 0.2)),
                      _MenuTile(
                        icon: Icons.logout_rounded,
                        title: 'Sign out',
                        titleColor: scheme.error,
                        iconColor: scheme.error,
                        onTap: context.read<AuthController>().signOut,
                      ),
                    ],
                  ),
                ).animate().fadeIn(delay: 300.ms).slideY(begin: 0.1),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _MenuTile extends StatelessWidget {
  const _MenuTile({
    required this.icon,
    required this.title,
    this.subtitle,
    required this.onTap,
    this.titleColor,
    this.iconColor,
  });

  final IconData icon;
  final String title;
  final String? subtitle;
  final VoidCallback onTap;
  final Color? titleColor;
  final Color? iconColor;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;

    return ListTile(
      contentPadding: const EdgeInsets.symmetric(horizontal: 24, vertical: 12),
      leading: Container(
        padding: const EdgeInsets.all(10),
        decoration: BoxDecoration(
          color: (iconColor ?? scheme.primary).withValues(alpha: 0.1),
          shape: BoxShape.circle,
        ),
        child: Icon(icon, color: iconColor ?? scheme.primary, size: 22),
      ),
      title: Text(
        title,
        style: theme.textTheme.titleMedium?.copyWith(
          color: titleColor ?? scheme.onSurface,
          fontWeight: FontWeight.w700,
        ),
      ),
      subtitle: subtitle != null
          ? Text(
              subtitle!,
              style: theme.textTheme.bodySmall?.copyWith(color: scheme.onSurfaceVariant),
            )
          : null,
      trailing: Icon(Icons.chevron_right_rounded, color: scheme.onSurfaceVariant.withValues(alpha: 0.5)),
      onTap: onTap,
    );
  }
}

class _AccountOnlyHeader extends StatelessWidget {
  const _AccountOnlyHeader();

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;
    final username = context.watch<AuthController>().principal?.displayName ?? '';

    return Row(
      children: [
        Container(
          width: 72,
          height: 72,
          decoration: BoxDecoration(
            color: scheme.surfaceContainerHighest,
            shape: BoxShape.circle,
            border: Border.all(color: scheme.outlineVariant.withValues(alpha: 0.2), width: 2),
          ),
          child: Icon(Icons.person_rounded, size: 36, color: scheme.onSurfaceVariant),
        ),
        const SizedBox(width: 20),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                username,
                style: theme.textTheme.headlineSmall?.copyWith(fontWeight: FontWeight.w800),
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
              ),
              const SizedBox(height: 4),
              Text(
                'Signed in',
                style: theme.textTheme.bodyMedium?.copyWith(
                  color: scheme.onSurfaceVariant,
                  fontWeight: FontWeight.w500,
                ),
              ),
            ],
          ),
        ),
      ],
    );
  }
}

class _Header extends StatelessWidget {
  const _Header({required this.profile});

  final MyProfile profile;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;

    return Row(
      children: [
        Container(
          width: 72,
          height: 72,
          decoration: BoxDecoration(
            gradient: LinearGradient(
              begin: Alignment.topLeft,
              end: Alignment.bottomRight,
              colors: [scheme.primary, scheme.primary.withValues(alpha: 0.7)],
            ),
            shape: BoxShape.circle,
            boxShadow: [
              BoxShadow(
                color: scheme.primary.withValues(alpha: 0.3),
                blurRadius: 16,
                offset: const Offset(0, 4),
              ),
            ],
          ),
          child: Center(
            child: Text(
              initialsOf(profile.fullName),
              style: theme.textTheme.headlineSmall?.copyWith(
                color: scheme.onPrimary,
                fontWeight: FontWeight.w800,
              ),
            ),
          ),
        ),
        const SizedBox(width: 20),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                profile.fullName,
                style: theme.textTheme.headlineSmall?.copyWith(fontWeight: FontWeight.w800),
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
              ),
              const SizedBox(height: 8),
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
                decoration: BoxDecoration(
                  color: scheme.primaryContainer.withValues(alpha: 0.5),
                  borderRadius: BorderRadius.circular(999),
                  border: Border.all(color: scheme.primary.withValues(alpha: 0.1)),
                ),
                child: Text(
                  profile.patientCode,
                  style: theme.textTheme.labelMedium?.copyWith(
                    color: scheme.primary,
                    fontWeight: FontWeight.w800,
                    letterSpacing: 1.5,
                  ),
                ),
              ),
            ],
          ),
        ),
      ],
    );
  }
}

class _EmergencyContact extends StatelessWidget {
  const _EmergencyContact({required this.profile});

  final MyProfile profile;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;
    final phone = profile.emergencyContactPhone;
    final name = profile.emergencyContactName;

    if (phone == null && name == null) {
      return SectionCard(
        title: 'Emergency contact',
        icon: Icons.emergency_outlined,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'No emergency contact has been added.',
              style: theme.textTheme.bodyMedium?.copyWith(
                color: scheme.onSurfaceVariant,
                height: 1.4,
              ),
            ),
            const SizedBox(height: 16),
            OutlinedButton.icon(
              onPressed: () => openMyDetails(context, context.read<ProfileController>()),
              icon: const Icon(Icons.add),
              label: const Text('Add a contact'),
              style: OutlinedButton.styleFrom(minimumSize: const Size(0, 48)),
            ),
          ],
        ),
      );
    }

    return SectionCard(
      title: 'Emergency contact',
      icon: Icons.emergency_outlined,
      padding: const EdgeInsets.all(20),
      child: Row(
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  name ?? 'Contact',
                  style: theme.textTheme.titleMedium?.copyWith(fontWeight: FontWeight.w700),
                ),
                if (phone != null) ...[
                  const SizedBox(height: 4),
                  Text(
                    phone,
                    style: theme.textTheme.bodyMedium?.copyWith(
                      color: scheme.onSurfaceVariant,
                      fontWeight: FontWeight.w600,
                      letterSpacing: 0.5,
                    ),
                  ),
                ],
              ],
            ),
          ),
          if (phone != null)
            FilledButton.icon(
              onPressed: () => callNumber(context, phone),
              icon: const Icon(Icons.call_rounded, size: 18),
              label: const Text('Call'),
              style: FilledButton.styleFrom(
                minimumSize: const Size(0, 48),
                shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(999)),
              ),
            ),
        ],
      ),
    );
  }
}
