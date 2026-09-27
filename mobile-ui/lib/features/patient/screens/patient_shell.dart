import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/widgets/async_data.dart';
import '../../../core/widgets/async_view.dart';
import '../../../core/widgets/phone_width.dart';
import '../../../services/api_client/models/my_profile.dart';
import '../../equipment/screens/my_prescriptions_screen.dart';
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

  // The tab itself is Equipment Management's - the pharmacy is theirs.
  final _prescriptionsKey = GlobalKey<MyPrescriptionsTabState>();

  static const _bar = [
    (tab: PatientTab.home, icon: Icons.home_outlined, on: Icons.home, label: 'Home'),
    (
      tab: PatientTab.appointments,
      icon: Icons.event_outlined,
      on: Icons.event,
      label: 'My visits'
    ),
    (
      tab: PatientTab.myStay,
      icon: Icons.monitor_heart_outlined,
      on: Icons.monitor_heart,
      label: 'My stay'
    ),
    (
      tab: PatientTab.prescriptions,
      icon: Icons.medication_outlined,
      on: Icons.medication,
      label: 'Prescriptions'
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
      case PatientTab.prescriptions:
        _prescriptionsKey.currentState?.refresh();
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
                  HomeScreen(onOpenTab: _openTab, onBookVisit: _bookVisit),
                  AppointmentsScreen(key: _appointmentsKey),
                  MyStayScreen(onBookVisit: _bookVisit),
                  MyPrescriptionsTab(key: _prescriptionsKey),
                  const ProfileScreen(),
                ],
              ),
            ),
            bottomNavigationBar: _PatientNavBar(
              selected: _tab,
              onSelect: _openTab,
            ),
          );
        },
      ),
    );
  }
}

// A hand-built bar rather than NavigationBar: that one gives every tab a fixed fifth of the width
// and wraps a long name ("Prescriptions") onto a second line. Here the open tab's name shrinks to
// fit instead, and the others show only their icon, named by a long-press tooltip.
class _PatientNavBar extends StatelessWidget {
  const _PatientNavBar({required this.selected, required this.onSelect});

  final PatientTab selected;
  final void Function(PatientTab tab) onSelect;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return DecoratedBox(
      decoration: BoxDecoration(
        color: theme.navigationBarTheme.backgroundColor,
        border: Border(top: BorderSide(color: theme.dividerColor)),
      ),
      child: SafeArea(
        top: false,
        child: SizedBox(
          height: 76,
          child: Row(
            children: [
              for (final item in _PatientShellState._bar)
                Expanded(
                  child: _NavItem(
                    icon: item.icon,
                    selectedIcon: item.on,
                    label: item.label,
                    selected: item.tab == selected,
                    onTap: () => onSelect(item.tab),
                  ),
                ),
            ],
          ),
        ),
      ),
    );
  }
}

class _NavItem extends StatelessWidget {
  const _NavItem({
    required this.icon,
    required this.selectedIcon,
    required this.label,
    required this.selected,
    required this.onTap,
  });

  final IconData icon;
  final IconData selectedIcon;
  final String label;
  final bool selected;
  final VoidCallback onTap;

  static const _duration = Duration(milliseconds: 220);

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;

    return Semantics(
      button: true,
      selected: selected,
      label: label,
      excludeSemantics: true,
      child: Tooltip(
        message: label,
        child: InkResponse(
          onTap: onTap,
          radius: 36,
          highlightShape: BoxShape.rectangle,
          containedInkWell: true,
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              AnimatedContainer(
                duration: _duration,
                curve: Curves.easeOutCubic,
                width: 60,
                height: 32,
                decoration: BoxDecoration(
                  color: selected ? scheme.primaryContainer : Colors.transparent,
                  borderRadius: BorderRadius.circular(16),
                ),
                child: Icon(
                  selected ? selectedIcon : icon,
                  size: 24,
                  color: selected ? scheme.onPrimaryContainer : scheme.onSurfaceVariant,
                ),
              ),
              AnimatedSize(
                duration: _duration,
                curve: Curves.easeOutCubic,
                child: selected
                    ? Padding(
                        padding: const EdgeInsets.fromLTRB(4, 4, 4, 0),
                        child: FittedBox(
                          fit: BoxFit.scaleDown,
                          child: Text(
                            label,
                            maxLines: 1,
                            style: theme.textTheme.labelMedium?.copyWith(
                              color: scheme.onSurface,
                              fontWeight: FontWeight.w700,
                            ),
                          ),
                        ),
                      )
                    : const SizedBox(width: double.infinity),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
