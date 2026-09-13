import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/auth/auth_controller.dart';
import '../../../services/api_client/models/my_profile.dart';
import '../state/profile_controller.dart';
import 'my_details_screen.dart';
import 'past_visits_screen.dart';

class ProfileScreen extends StatelessWidget {
  const ProfileScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final profileController = context.watch<ProfileController>();
    final profile = profileController.profile.valueOrNull;

    if (profile == null) return const SizedBox.shrink();

    return Scaffold(
      appBar: AppBar(title: const Text('Profile')),
      body: ListView(
        children: [
          _Header(profile: profile),
          if (!profile.detailsComplete) _MissingDetails(missing: profile.missingFields),
          const Divider(height: 32),
          ListTile(
            leading: const Icon(Icons.edit_outlined),
            title: const Text('My details'),
            subtitle: const Text('Name, NIC, address, emergency contact'),
            trailing: const Icon(Icons.chevron_right),
            onTap: () => _openDetails(context, profileController),
          ),
          ListTile(
            leading: const Icon(Icons.history),
            title: const Text('Past visits'),
            subtitle: const Text('Stays that have finished'),
            trailing: const Icon(Icons.chevron_right),
            onTap: () => Navigator.of(context).push(
              MaterialPageRoute(builder: (_) => const PastVisitsScreen()),
            ),
          ),
          const Divider(height: 32),
          ListTile(
            leading: Icon(Icons.logout, color: Theme.of(context).colorScheme.error),
            title: Text(
              'Sign out',
              style: TextStyle(color: Theme.of(context).colorScheme.error),
            ),
            onTap: context.read<AuthController>().signOut,
          ),
        ],
      ),
    );
  }

  /// The details form reads and writes the same controller this screen watches,
  /// so it has to be carried across into the pushed route.
  void _openDetails(BuildContext context, ProfileController controller) {
    Navigator.of(context).push(MaterialPageRoute(
      builder: (_) => ChangeNotifierProvider<ProfileController>.value(
        value: controller,
        child: const MyDetailsScreen(),
      ),
    ));
  }
}

class _Header extends StatelessWidget {
  const _Header({required this.profile});

  final MyProfile profile;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 16, 16, 0),
      child: Row(
        children: [
          CircleAvatar(
            radius: 28,
            backgroundColor: theme.colorScheme.primaryContainer,
            child: Text(
              _initials(profile.fullName),
              style: theme.textTheme.titleMedium
                  ?.copyWith(color: theme.colorScheme.onPrimaryContainer),
            ),
          ),
          const SizedBox(width: 16),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(profile.fullName, style: theme.textTheme.titleLarge),
                const SizedBox(height: 2),
                Text(
                  profile.patientCode,
                  style: theme.textTheme.bodySmall
                      ?.copyWith(color: theme.colorScheme.onSurfaceVariant),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  static String _initials(String name) {
    final parts = name.trim().split(RegExp(r'\s+')).where((p) => p.isNotEmpty).toList();
    if (parts.isEmpty) return '?';
    if (parts.length == 1) return parts.first.characters.first.toUpperCase();
    return (parts.first.characters.first + parts.last.characters.first).toUpperCase();
  }
}

class _MissingDetails extends StatelessWidget {
  const _MissingDetails({required this.missing});

  final List<String> missing;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 16, 16, 0),
      child: Card(
        color: theme.colorScheme.tertiaryContainer,
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                'Some details are still missing',
                style: theme.textTheme.titleSmall
                    ?.copyWith(color: theme.colorScheme.onTertiaryContainer),
              ),
              const SizedBox(height: 8),
              ...missing.map((field) => Text(
                    '• $field',
                    style: theme.textTheme.bodySmall
                        ?.copyWith(color: theme.colorScheme.onTertiaryContainer),
                  )),
            ],
          ),
        ),
      ),
    );
  }
}
