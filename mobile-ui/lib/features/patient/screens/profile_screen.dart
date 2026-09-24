import 'package:flutter/material.dart';
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
    final scheme = Theme.of(context).colorScheme;

    return Scaffold(
      appBar: AppBar(title: const Text('Profile')),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(AppTheme.gutter, 4, AppTheme.gutter, 32),
        children: [
          if (profile == null) ...[
            const _AccountOnlyHeader(),
            const SizedBox(height: 20),
            NoticeBanner(
              icon: Icons.badge_outlined,
              accent: scheme.warning,
              title: 'No hospital record',
              body: 'Add your details to create your hospital record. If the hospital has '
                  'already registered you at the desk, use your patient code instead so '
                  'your stay and history come with you.',
              action: Wrap(
                spacing: 10,
                runSpacing: 8,
                children: [
                  FilledButton(
                    onPressed: () => openMyDetails(context, profileController),
                    style: FilledButton.styleFrom(minimumSize: const Size(0, 44)),
                    child: const Text('Add my details'),
                  ),
                  OutlinedButton(
                    onPressed: () => openClaimRecord(context, profileController),
                    style: OutlinedButton.styleFrom(minimumSize: const Size(0, 44)),
                    child: const Text('I have a patient code'),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 16),
          ] else ...[
            _Header(profile: profile),
            const SizedBox(height: 20),
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
                    padding: const EdgeInsets.symmetric(horizontal: 18),
                  ),
                  child: const Text('Add them now'),
                ),
              ),
              const SizedBox(height: 16),
            ],
            SectionCard(
              title: 'Your details',
              icon: Icons.badge_outlined,
              trailing: TextButton(
                onPressed: () => openMyDetails(context, profileController),
                child: const Text('Edit'),
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
            ),
            const SizedBox(height: 16),
            _EmergencyContact(profile: profile),
            const SizedBox(height: 16),
          ],
          Card(
            child: Column(
              children: [
                ListTile(
                  leading: const Icon(Icons.history),
                  title: const Text('Past visits'),
                  subtitle: const Text('Completed stays'),
                  trailing: const Icon(Icons.chevron_right),
                  onTap: () => openPastVisits(context),
                ),
                const Divider(indent: 20, endIndent: 20),
                ListTile(
                  leading: const Icon(Icons.description_outlined),
                  title: const Text('My reports'),
                  subtitle: const Text('Lab results'),
                  trailing: const Icon(Icons.chevron_right),
                  onTap: () => Navigator.of(
                    context,
                  ).push(MaterialPageRoute(builder: (_) => const MyReportsScreen())),
                ),
                const Divider(indent: 20, endIndent: 20),
                ListTile(
                  leading: Icon(Icons.logout, color: scheme.error),
                  title: Text('Sign out', style: TextStyle(color: scheme.error)),
                  onTap: context.read<AuthController>().signOut,
                ),
              ],
            ),
          ),
        ],
      ),
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
        CircleAvatar(
          radius: 30,
          backgroundColor: scheme.surfaceContainerHighest,
          child: Icon(Icons.person_outline, size: 30, color: scheme.onSurfaceVariant),
        ),
        const SizedBox(width: 16),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                username,
                style: theme.textTheme.titleLarge,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
              ),
              const SizedBox(height: 4),
              Text(
                'Signed in',
                style: theme.textTheme.bodySmall?.copyWith(color: scheme.onSurfaceVariant),
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
        CircleAvatar(
          radius: 30,
          backgroundColor: scheme.primaryContainer,
          child: Text(
            initialsOf(profile.fullName),
            style: theme.textTheme.titleLarge?.copyWith(color: scheme.onPrimaryContainer),
          ),
        ),
        const SizedBox(width: 16),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                profile.fullName,
                style: theme.textTheme.titleLarge,
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
              ),
              const SizedBox(height: 4),
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                decoration: BoxDecoration(
                  color: scheme.surfaceContainerHighest,
                  borderRadius: BorderRadius.circular(999),
                ),
                child: Text(
                  profile.patientCode,
                  style: theme.textTheme.labelMedium?.copyWith(
                    color: scheme.onSurfaceVariant,
                    letterSpacing: 1,
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
        title: 'Emergency/guardian contact',
        icon: Icons.emergency_outlined,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'No emergency contact has been added.',
              style: theme.textTheme.bodyMedium?.copyWith(color: scheme.onSurfaceVariant),
            ),
            const SizedBox(height: 14),
            OutlinedButton.icon(
              onPressed: () => openMyDetails(context, context.read<ProfileController>()),
              icon: const Icon(Icons.add),
              label: const Text('Add a contact'),
              style: OutlinedButton.styleFrom(minimumSize: const Size(0, 44)),
            ),
          ],
        ),
      );
    }

    return SectionCard(
      title: 'Emergency/guardian contact',
      icon: Icons.emergency_outlined,
      child: Row(
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(name ?? 'Contact', style: theme.textTheme.titleSmall),
                if (phone != null) ...[
                  const SizedBox(height: 2),
                  Text(
                    phone,
                    style: theme.textTheme.bodyMedium?.copyWith(color: scheme.onSurfaceVariant),
                  ),
                ],
              ],
            ),
          ),
          if (phone != null)
            FilledButton.tonalIcon(
              onPressed: () => callNumber(context, phone),
              icon: const Icon(Icons.call, size: 18),
              label: const Text('Call'),
              style: FilledButton.styleFrom(minimumSize: const Size(0, 44)),
            ),
        ],
      ),
    );
  }
}
