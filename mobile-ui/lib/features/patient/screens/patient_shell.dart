import 'package:flutter/material.dart';
import 'package:flutter_animate/flutter_animate.dart';
import 'package:provider/provider.dart';

import '../../../core/theme/app_theme.dart';
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
    (tab: PatientTab.home, icon: Icons.home_outlined, on: Icons.home_rounded, label: 'Home'),
    (
      tab: PatientTab.appointments,
      icon: Icons.event_outlined,
      on: Icons.event_rounded,
      label: 'Visits'
    ),
    (
      tab: PatientTab.myStay,
      icon: Icons.monitor_heart_outlined,
      on: Icons.monitor_heart_rounded,
      label: 'My Stay'
    ),
    (
      tab: PatientTab.prescriptions,
      icon: Icons.medication_outlined,
      on: Icons.medication_rounded,
      label: 'Rx'
    ),
    (tab: PatientTab.profile, icon: Icons.person_outline, on: Icons.person_rounded, label: 'Profile'),
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
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;

    return PhoneWidth(
      child: AsyncView<MyProfile?>(
        state: profile.profile,
        onRetry: profile.load,
        builder: (context, _) {
          return Scaffold(
            extendBody: true, // Content flows behind the floating bar
            body: SafeArea(
              bottom: false,
              child: IndexedStack(
                index: _tab.index,
                children: [
                  HomeScreen(onOpenTab: _openTab),
                  AppointmentsScreen(key: _appointmentsKey),
                  MyStayScreen(onBookVisit: _bookVisit),
                  MyPrescriptionsTab(key: _prescriptionsKey),
                  const ProfileScreen(),
                ],
              ),
            ),
            bottomNavigationBar: SafeArea(
              child: Container(
                margin: const EdgeInsets.fromLTRB(20, 0, 20, 20),
                padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 8),
                decoration: BoxDecoration(
                  color: scheme.surface,
                  borderRadius: BorderRadius.circular(36),
                  boxShadow: [
                    BoxShadow(
                      color: scheme.shadow.withValues(alpha: 0.1),
                      blurRadius: 24,
                      offset: const Offset(0, 8),
                    ),
                  ],
                  border: Border.all(
                    color: scheme.outlineVariant.withValues(alpha: 0.2),
                  ),
                ),
                child: Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    for (final item in _bar)
                      _NavItem(
                        item: item,
                        isSelected: _tab == item.tab,
                        onTap: () => _openTab(item.tab),
                      ),
                  ],
                ),
              ).animate().fadeIn(duration: 500.ms).slideY(begin: 0.5, curve: Curves.easeOutCubic),
            ),
          );
        },
      ),
    );
  }
}

class _NavItem extends StatelessWidget {
  const _NavItem({
    required this.item,
    required this.isSelected,
    required this.onTap,
  });

  final ({PatientTab tab, IconData icon, IconData on, String label}) item;
  final bool isSelected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;

    return GestureDetector(
      onTap: onTap,
      behavior: HitTestBehavior.opaque,
      child: AnimatedContainer(
        duration: const Duration(milliseconds: 300),
        curve: Curves.easeOutQuint,
        padding: EdgeInsets.symmetric(
          horizontal: isSelected ? 16 : 12,
          vertical: 10,
        ),
        decoration: BoxDecoration(
          color: isSelected ? scheme.primaryContainer : Colors.transparent,
          borderRadius: BorderRadius.circular(24),
        ),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(
              isSelected ? item.on : item.icon,
              color: isSelected ? scheme.primary : scheme.onSurfaceVariant,
              size: 24,
            ),
            if (isSelected) ...[
              const SizedBox(width: 6),
              Text(
                item.label,
                style: theme.textTheme.labelSmall?.copyWith(
                  color: scheme.primary,
                  fontWeight: FontWeight.w700,
                  letterSpacing: 0.2,
                ),
              ).animate().fadeIn(duration: 200.ms).slideX(begin: -0.2),
            ],
          ],
        ),
      ),
    );
  }
}
