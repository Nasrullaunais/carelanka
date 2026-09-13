import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/widgets/async_view.dart';
import '../../../services/api_client/models/my_profile.dart';
import '../state/profile_controller.dart';
import 'appointments_screen.dart';
import 'my_details_screen.dart';
import 'my_stay_screen.dart';
import 'profile_screen.dart';

/// The patient area: my stay, appointments and profile behind a bottom bar.
///
/// A new account has no hospital record behind it, and without one there is no
/// stay to show and booking a visit is refused. The record is loaded once here
/// so each tab does not have to discover that for itself.
class PatientShell extends StatefulWidget {
  const PatientShell({super.key});

  @override
  State<PatientShell> createState() => _PatientShellState();
}

class _PatientShellState extends State<PatientShell> {
  int _tab = 0;

  static const _tabs = [
    _Tab(icon: Icons.bed_outlined, selected: Icons.bed, label: 'My stay'),
    _Tab(icon: Icons.event_outlined, selected: Icons.event, label: 'Appointments'),
    _Tab(icon: Icons.person_outline, selected: Icons.person, label: 'Profile'),
  ];

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      context.read<ProfileController>().load();
    });
  }

  @override
  Widget build(BuildContext context) {
    final profile = context.watch<ProfileController>();

    return AsyncView<MyProfile?>(
      state: profile.profile,
      onRetry: profile.load,
      builder: (context, record) {
        if (record == null) return const MyDetailsScreen(firstTime: true);

        return Scaffold(
          body: IndexedStack(
            index: _tab,
            children: const [MyStayScreen(), AppointmentsScreen(), ProfileScreen()],
          ),
          bottomNavigationBar: NavigationBar(
            selectedIndex: _tab,
            onDestinationSelected: (index) => setState(() => _tab = index),
            destinations: [
              for (final tab in _tabs)
                NavigationDestination(
                  icon: Icon(tab.icon),
                  selectedIcon: Icon(tab.selected),
                  label: tab.label,
                ),
            ],
          ),
        );
      },
    );
  }
}

class _Tab {
  const _Tab({required this.icon, required this.selected, required this.label});

  final IconData icon;
  final IconData selected;
  final String label;
}
