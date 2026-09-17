import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/widgets/async_data.dart';
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

class PatientShell extends StatefulWidget {
  const PatientShell({super.key});

  @override
  State<PatientShell> createState() => _PatientShellState();
}

class _PatientShellState extends State<PatientShell> {
  PatientTab _tab = PatientTab.home;

  late final ProfileController _profile;

  bool? _wasLinked;

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
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted) return;
      context.read<ProfileController>().load();
      context.read<MyStayController>().load();
      context.read<AppointmentsController>().load();
    });

    _profile = context.read<ProfileController>();
    _profile.addListener(_onProfileChanged);
  }

  @override
  void dispose() {
    _profile.removeListener(_onProfileChanged);
    super.dispose();
  }

  // Refetches stay/appointments once a first-time save creates the hospital record — both were fetched before it existed.
  void _onProfileChanged() {
    if (_profile.profile is AsyncLoading) return;

    final linked = _profile.isLinked;
    final previous = _wasLinked;
    _wasLinked = linked;

    if (previous == false && linked) {
      context.read<MyStayController>().load();
      context.read<AppointmentsController>().load();
    }
  }

  // Staff change the stay from the other side of the hospital, so opening a tab refetches what
  // that tab shows. Silently: the screen keeps what it has until the new answer arrives.
  void _openTab(PatientTab tab) {
    setState(() => _tab = tab);
    _refresh(tab);
  }

  void _refresh(PatientTab tab) {
    switch (tab) {
      case PatientTab.home:
        context.read<ProfileController>().load(showLoading: false);
        context.read<MyStayController>().load(showLoading: false);
        context.read<AppointmentsController>().load(showLoading: false);
      case PatientTab.appointments:
        context.read<AppointmentsController>().load(showLoading: false);
      case PatientTab.myStay:
        context.read<MyStayController>().load(showLoading: false);
      case PatientTab.profile:
        context.read<ProfileController>().load(showLoading: false);
    }
  }

  void _bookVisit() {
    setState(() => _tab = PatientTab.appointments);
    WidgetsBinding.instance.addPostFrameCallback((_) {
      _appointmentsKey.currentState?.book();
    });
  }

  @override
  Widget build(BuildContext context) {
    final profile = context.watch<ProfileController>();

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
