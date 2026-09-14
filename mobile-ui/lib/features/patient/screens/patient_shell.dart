import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/widgets/async_view.dart';
import '../../../core/widgets/phone_width.dart';
import '../../../services/api_client/models/my_profile.dart';
import '../state/appointments_controller.dart';
import '../state/my_stay_controller.dart';
import '../state/profile_controller.dart';
import 'appointments_screen.dart';
import 'home_screen.dart';
import 'my_stay_screen.dart';
import 'profile_screen.dart';

/// The patient area: home, appointments, stay and profile behind a bottom bar.
///
/// A new account has no hospital record behind it, and without one there is no
/// stay to show and booking a visit is refused. The tabs still open - each one
/// says why it is empty and offers the details form - because a blocking form
/// with no way past it strands anyone who signed up with the wrong account.
/// The record is loaded once here so each tab does not have to discover that
/// for itself.
class PatientShell extends StatefulWidget {
  const PatientShell({super.key});

  @override
  State<PatientShell> createState() => _PatientShellState();
}

class _PatientShellState extends State<PatientShell> {
  PatientTab _tab = PatientTab.home;

  /// Home's "Book a visit" opens the sheet that Appointments owns, so the
  /// booking flow exists once. Reaching it needs that screen's state.
  final _appointmentsKey = GlobalKey<AppointmentsScreenState>();

  static const _bar = [
    (tab: PatientTab.home, icon: Icons.home_outlined, on: Icons.home, label: 'Home'),
    (
      tab: PatientTab.appointments,
      icon: Icons.event_outlined,
      on: Icons.event,
      label: 'Appointments'
    ),
    (
      tab: PatientTab.myStay,
      icon: Icons.monitor_heart_outlined,
      on: Icons.monitor_heart,
      label: 'My stay'
    ),
    (tab: PatientTab.profile, icon: Icons.person_outline, on: Icons.person, label: 'Profile'),
  ];

  @override
  void initState() {
    super.initState();
    // Every tab reads from these, and Home reads from all three at once, so
    // they are loaded here rather than by whichever screen happens to build
    // first.
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted) return;
      context.read<ProfileController>().load();
      context.read<MyStayController>().load();
      context.read<AppointmentsController>().load();
    });
  }

  void _openTab(PatientTab tab) => setState(() => _tab = tab);

  /// Jumps to Appointments and opens the booking sheet on top of it, so the
  /// patient lands on the list their new booking will appear in.
  void _bookVisit() {
    setState(() => _tab = PatientTab.appointments);
    WidgetsBinding.instance.addPostFrameCallback((_) {
      _appointmentsKey.currentState?.book();
    });
  }

  @override
  Widget build(BuildContext context) {
    final profile = context.watch<ProfileController>();

    // Wraps everything, not just the tabs — the loading state is part of the
    // same app and should sit in the same frame.
    return PhoneWidth(
      child: AsyncView<MyProfile?>(
        state: profile.profile,
        onRetry: profile.load,
        builder: (context, _) {
          return Scaffold(
            body: SafeArea(
              bottom: false,
              child: IndexedStack(
                index: _tab.index,
                children: [
                  HomeScreen(onOpenTab: _openTab),
                  AppointmentsScreen(key: _appointmentsKey),
                  MyStayScreen(onBookVisit: _bookVisit),
                  const ProfileScreen(),
                ],
              ),
            ),
            bottomNavigationBar: NavigationBar(
              selectedIndex: _tab.index,
              onDestinationSelected: (index) => _openTab(PatientTab.values[index]),
              destinations: [
                for (final item in _bar)
                  NavigationDestination(
                    icon: Icon(item.icon),
                    selectedIcon: Icon(item.on),
                    label: item.label,
                  ),
              ],
            ),
          );
        },
      ),
    );
  }
}
