import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/notifications/notification_bell.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/utils/dialer.dart';
import '../../../core/utils/friendly_date.dart';
import '../../../core/widgets/async_data.dart';
import '../../../core/widgets/async_view.dart';
import '../../../core/widgets/notice_banner.dart';
import '../../../services/api_client/models/my_admission.dart';
import '../../../services/api_client/models/my_appointment.dart';
import '../../../services/api_client/models/my_profile.dart';
import '../../emergency/widgets/ambulance_request_card.dart';
import '../state/appointments_controller.dart';
import '../state/my_stay_controller.dart';
import '../state/profile_controller.dart';
import '../widgets/stay_journey.dart';
import 'claim_record_screen.dart';
import 'my_details_screen.dart';
import 'my_reports_screen.dart';
import 'past_visits_screen.dart';

class HomeScreen extends StatelessWidget {
  const HomeScreen({super.key, required this.onOpenTab, this.onBookVisit});

  final void Function(PatientTab tab) onOpenTab;

  // Opens the booking form itself; without it the shortcut can only switch to the visits tab.
  final VoidCallback? onBookVisit;

  @override
  Widget build(BuildContext context) {
    final profile = context.watch<ProfileController>().profile.valueOrNull;
    final stay = context.watch<MyStayController>();
    final appointments = context.watch<AppointmentsController>();
    final admitted = stay.state.valueOrNull is MyStayCurrent;
    final wash = BrandSurfaces.of(context).pageWash;

    return Scaffold(
      body: DecoratedBox(
        decoration: BoxDecoration(
          gradient: wash == null
              ? null
              : LinearGradient(
                  begin: Alignment.topCenter,
                  end: Alignment.bottomCenter,
                  stops: const [0, 0.32],
                  colors: [wash, Theme.of(context).scaffoldBackgroundColor],
                ),
        ),
        child: RefreshIndicator(
          onRefresh: () async {
            await Future.wait([
              context.read<ProfileController>().load(showLoading: false),
              stay.load(showLoading: false),
              appointments.load(showLoading: false),
            ]);
          },
          child: ListView(
            padding: const EdgeInsets.fromLTRB(AppTheme.gutter, 16, AppTheme.gutter, 32),
            children: profile == null
                ? const [_NotLinkedYet()]
                : [
                    _Greeting(
                      profile: profile,
                      onOpenProfile: () => onOpenTab(PatientTab.profile),
                    ),
                    const SizedBox(height: 22),
                    _WhatsNext(
                      stay: stay.state,
                      nextVisit: appointments.upcoming.isEmpty
                          ? null
                          : appointments.upcoming.first,
                      onOpenTab: onOpenTab,
                    ),
                    if (!profile.detailsComplete) ...[
                      const SizedBox(height: 16),
                      _CompleteDetailsBanner(missing: profile.missingFields),
                    ],
                    const SizedBox(height: 22),
                    _Shortcuts(
                      admitted: admitted,
                      onOpenTab: onOpenTab,
                      onBookVisit: onBookVisit,
                    ),
                    const SizedBox(height: 24),
                    const AmbulanceRequestCard(),
                    const SizedBox(height: 28),
                    Padding(
                      padding: const EdgeInsets.only(left: 2, bottom: 12),
                      child: Text(
                        'Emergency contact',
                        style: Theme.of(context).textTheme.titleMedium,
                      ),
                    ),
                    _EmergencyContactCard(profile: profile),
                  ],
          ),
        ),
      ),
    );
  }
}

class _NotLinkedYet extends StatelessWidget {
  const _NotLinkedYet();

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(_timeOfDayGreeting(), style: theme.textTheme.headlineMedium),
        const SizedBox(height: 20),
        NoticeBanner(
          icon: Icons.badge_outlined,
          accent: theme.colorScheme.warning,
          title: 'Complete your registration',
          body:
              'Add your details to book visits and view your stay. If the hospital has '
              'already registered you at the desk, use your patient code instead so your '
              'stay and history come with you.',
          action: Wrap(
            spacing: 10,
            runSpacing: 8,
            children: [
              FilledButton(
                onPressed: () => openMyDetails(context, context.read<ProfileController>()),
                style: FilledButton.styleFrom(minimumSize: const Size(0, 44)),
                child: const Text('Add my details'),
              ),
              OutlinedButton(
                onPressed: () => openClaimRecord(context, context.read<ProfileController>()),
                style: OutlinedButton.styleFrom(minimumSize: const Size(0, 44)),
                child: const Text('I have a patient code'),
              ),
            ],
          ),
        ),
      ],
    );
  }
}

enum PatientTab { home, appointments, myStay, prescriptions, profile }

class _Greeting extends StatelessWidget {
  const _Greeting({required this.profile, required this.onOpenProfile});

  final MyProfile profile;
  final VoidCallback onOpenProfile;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;

    return Row(
      children: [
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                _timeOfDayGreeting(),
                style: theme.textTheme.bodyLarge?.copyWith(
                  color: scheme.onSurfaceVariant,
                  fontWeight: FontWeight.w500,
                ),
              ),
              Text(
                _firstName(profile.fullName),
                style: theme.textTheme.headlineMedium?.copyWith(
                  fontSize: 30,
                  fontWeight: FontWeight.w800,
                  height: 1.15,
                ),
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
              ),
            ],
          ),
        ),
        const NotificationBell(),
        Tooltip(
          message: 'Profile',
          child: Material(
            color: scheme.surface,
            shape: CircleBorder(
              side: BorderSide(color: scheme.primary.withValues(alpha: 0.5), width: 2),
            ),
            clipBehavior: Clip.antiAlias,
            child: InkWell(
              onTap: onOpenProfile,
              child: SizedBox(
                width: 48,
                height: 48,
                child: Center(
                  child: Text(
                    initialsOf(profile.fullName),
                    style: theme.textTheme.titleMedium?.copyWith(color: scheme.primary),
                  ),
                ),
              ),
            ),
          ),
        ),
      ],
    );
  }

  static String _firstName(String fullName) {
    final parts = fullName.trim().split(RegExp(r'\s+'));
    return parts.isEmpty ? fullName : parts.first;
  }
}

String _timeOfDayGreeting() {
  final hour = DateTime.now().hour;
  if (hour < 12) return 'Good morning';
  if (hour < 17) return 'Good afternoon';
  return 'Good evening';
}

class _WhatsNext extends StatelessWidget {
  const _WhatsNext({required this.stay, required this.nextVisit, required this.onOpenTab});

  final AsyncData<MyStay> stay;
  final MyAppointment? nextVisit;
  final void Function(PatientTab tab) onOpenTab;

  @override
  Widget build(BuildContext context) {
    return AnimatedSwitcher(
      duration: const Duration(milliseconds: 220),
      switchInCurve: Curves.easeOutCubic,
      child: switch (stay) {
        AsyncLoading<MyStay>() => const Skeleton.card(height: 170),
        AsyncReady<MyStay>(value: MyStayCurrent(:final admission)) => _StayHero(
          admission: admission,
          onTap: () => onOpenTab(PatientTab.myStay),
        ),
        _ =>
          nextVisit == null
              ? const _NoVisitCard()
              : _VisitHero(visit: nextVisit!, onTap: () => onOpenTab(PatientTab.appointments)),
      },
    );
  }
}

class _StayHero extends StatelessWidget {
  const _StayHero({required this.admission, required this.onTap});

  final MyAdmission admission;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final brand = BrandSurfaces.of(context);
    final journey = StayJourney.of(admission.status);
    final reached = journey.reached;
    final place = [admission.wardName, admission.bedNumber].whereType<String>().join(' · ');
    final missing = admission.missingFields.length;

    return _HeroCard(
      onTap: onTap,
      chipIcon: Icons.bed_outlined,
      chipLabel: 'Your stay',
      heading: admission.statusText,
      children: [
        if (place.isNotEmpty)
          Row(
            children: [
              Icon(Icons.place_outlined, size: 17, color: brand.onHeroMuted),
              const SizedBox(width: 6),
              Expanded(
                child: Text(
                  place,
                  style: theme.textTheme.bodyMedium?.copyWith(color: brand.onHeroMuted),
                ),
              ),
            ],
          ),
        if (reached != null) ...[
          _HeroProgress(current: reached.index, total: JourneyStep.values.length),
          Text(
            'Step ${reached.index + 1} of ${JourneyStep.values.length} · ${reached.detail}',
            style: theme.textTheme.bodyMedium?.copyWith(
              color: brand.onHeroMuted,
              fontWeight: FontWeight.w500,
            ),
          ),
        ],
        if (!admission.detailsComplete)
          _HeroChip(
            icon: Icons.info_outline,
            label: '$missing detail${missing == 1 ? '' : 's'} missing',
          ),
      ],
    );
  }
}

class _VisitHero extends StatelessWidget {
  const _VisitHero({required this.visit, required this.onTap});

  final MyAppointment visit;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final brand = BrandSurfaces.of(context);

    return _HeroCard(
      onTap: onTap,
      chipIcon: Icons.event_outlined,
      chipLabel: 'Next visit',
      heading: FriendlyDate.relativeDayAndTime(visit.scheduledAt),
      children: [
        Row(
          children: [
            _HeroChip(label: FriendlyDate.countdown(visit.scheduledAt)),
            const SizedBox(width: 10),
            Expanded(
              child: Text(
                visit.statusText,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: theme.textTheme.bodyMedium?.copyWith(color: brand.onHeroMuted),
              ),
            ),
          ],
        ),
        if (visit.reason != null)
          Text(
            visit.reason!,
            maxLines: 2,
            overflow: TextOverflow.ellipsis,
            style: theme.textTheme.bodyLarge?.copyWith(color: brand.onHero),
          ),
      ],
    );
  }
}

// The one gradient surface in the app: whatever is happening to the patient right now.
class _HeroCard extends StatelessWidget {
  const _HeroCard({
    required this.onTap,
    required this.chipIcon,
    required this.chipLabel,
    required this.heading,
    required this.children,
  });

  final VoidCallback onTap;
  final IconData chipIcon;
  final String chipLabel;
  final String heading;
  final List<Widget> children;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final brand = BrandSurfaces.of(context);
    final radius = BorderRadius.circular(26);

    return DecoratedBox(
      decoration: BoxDecoration(
        borderRadius: radius,
        gradient: LinearGradient(
          colors: [brand.heroFrom, brand.heroTo],
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
        ),
        boxShadow: [
          BoxShadow(
            color: brand.heroGlow ?? const Color(0x14101828),
            blurRadius: brand.heroGlow == null ? 18 : 30,
            offset: Offset(0, brand.heroGlow == null ? 8 : 12),
          ),
        ],
      ),
      child: Material(
        type: MaterialType.transparency,
        borderRadius: radius,
        clipBehavior: Clip.antiAlias,
        child: InkWell(
          onTap: onTap,
          child: Padding(
            padding: const EdgeInsets.fromLTRB(20, 18, 20, 20),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    _HeroChip(icon: chipIcon, label: chipLabel),
                    const Spacer(),
                    Icon(Icons.arrow_forward_rounded, color: brand.onHero),
                  ],
                ),
                const SizedBox(height: 14),
                Text(
                  heading,
                  style: theme.textTheme.headlineSmall?.copyWith(
                    fontSize: 25,
                    height: 1.2,
                    letterSpacing: -0.4,
                    color: brand.onHero,
                  ),
                ),
                for (final child in children) ...[const SizedBox(height: 12), child],
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _HeroChip extends StatelessWidget {
  const _HeroChip({required this.label, this.icon});

  final String label;
  final IconData? icon;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final brand = BrandSurfaces.of(context);

    return Align(
      alignment: Alignment.centerLeft,
      widthFactor: 1,
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
        decoration: BoxDecoration(
          color: brand.heroChip,
          borderRadius: BorderRadius.circular(99),
        ),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            if (icon != null) ...[
              Icon(icon, size: 15, color: brand.onHero),
              const SizedBox(width: 6),
            ],
            Text(
              label,
              style: theme.textTheme.labelMedium?.copyWith(
                color: brand.onHero,
                fontWeight: FontWeight.w700,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _HeroProgress extends StatelessWidget {
  const _HeroProgress({required this.current, required this.total});

  final int current;
  final int total;

  @override
  Widget build(BuildContext context) {
    final brand = BrandSurfaces.of(context);

    return Row(
      children: [
        for (var i = 0; i < total; i++) ...[
          Expanded(
            child: Container(
              height: 6,
              decoration: BoxDecoration(
                color: i <= current ? brand.onHero : brand.heroTrack,
                borderRadius: BorderRadius.circular(9),
              ),
            ),
          ),
          if (i < total - 1) const SizedBox(width: 6),
        ],
      ],
    );
  }
}

class _NoVisitCard extends StatelessWidget {
  const _NoVisitCard();

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;
    final (tint, ink) = BrandSurfaces.of(context).accents[0];

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Row(
          children: [
            Container(
              width: 52,
              height: 52,
              decoration: BoxDecoration(color: tint, borderRadius: BorderRadius.circular(18)),
              child: Icon(Icons.event_available_outlined, color: ink),
            ),
            const SizedBox(width: 16),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('No upcoming visits', style: theme.textTheme.titleMedium),
                  const SizedBox(height: 2),
                  Text(
                    'Book a visit when you need to be seen.',
                    style: theme.textTheme.bodyMedium?.copyWith(color: scheme.onSurfaceVariant),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _CompleteDetailsBanner extends StatelessWidget {
  const _CompleteDetailsBanner({required this.missing});

  final List<String> missing;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final controller = context.read<ProfileController>();

    return NoticeBanner(
      icon: Icons.info_outline,
      accent: scheme.warning,
      title: 'Incomplete details',
      body: 'The hospital requires the following before your next visit.',
      bullets: missing.map(prettyFieldName).toList(),
      action: OutlinedButton(
        onPressed: () => openMyDetails(context, controller),
        style: OutlinedButton.styleFrom(
          minimumSize: const Size(0, 44),
          padding: const EdgeInsets.symmetric(horizontal: 18),
        ),
        child: const Text('Add them now'),
      ),
    );
  }
}

class _Shortcuts extends StatelessWidget {
  const _Shortcuts({required this.admitted, required this.onOpenTab, required this.onBookVisit});

  // While admitted the API refuses new bookings and past stays are not what the patient is
  // after, so those two give way to Profile.
  final bool admitted;
  final void Function(PatientTab tab) onOpenTab;
  final VoidCallback? onBookVisit;

  @override
  Widget build(BuildContext context) {
    final accents = BrandSurfaces.of(context).accents;
    void openReports() =>
        Navigator.of(context).push(MaterialPageRoute(builder: (_) => const MyReportsScreen()));

    final items = [
      if (!admitted)
        (
          Icons.calendar_month_rounded,
          'Book a visit',
          onBookVisit ?? () => onOpenTab(PatientTab.appointments),
        ),
      (Icons.description_outlined, 'My reports', openReports),
      (Icons.medication_outlined, 'Prescriptions', () => onOpenTab(PatientTab.prescriptions)),
      if (!admitted)
        (Icons.history_rounded, 'Past visits', () => openPastVisits(context))
      else
        (Icons.person_outline_rounded, 'Profile', () => onOpenTab(PatientTab.profile)),
    ];

    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        for (var i = 0; i < items.length; i++)
          Expanded(
            child: _ShortcutTile(
              icon: items[i].$1,
              label: items[i].$2,
              onTap: items[i].$3,
              colors: accents[i % accents.length],
            ),
          ),
      ],
    );
  }
}

class _ShortcutTile extends StatelessWidget {
  const _ShortcutTile({
    required this.icon,
    required this.label,
    required this.onTap,
    required this.colors,
  });

  final IconData icon;
  final String label;
  final VoidCallback onTap;
  final (Color, Color) colors;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Semantics(
      button: true,
      label: label,
      excludeSemantics: true,
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(AppTheme.radiusL),
        child: Padding(
          padding: const EdgeInsets.symmetric(vertical: 4, horizontal: 2),
          child: Column(
            children: [
              Container(
                width: 62,
                height: 62,
                decoration: BoxDecoration(
                  color: colors.$1,
                  borderRadius: BorderRadius.circular(20),
                ),
                child: Icon(icon, color: colors.$2, size: 27),
              ),
              const SizedBox(height: 8),
              FittedBox(
                fit: BoxFit.scaleDown,
                child: Text(
                  label,
                  maxLines: 1,
                  style: theme.textTheme.labelLarge?.copyWith(fontSize: 13),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _EmergencyContactCard extends StatelessWidget {
  const _EmergencyContactCard({required this.profile});

  final MyProfile profile;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;
    final accents = BrandSurfaces.of(context).accents;
    final phone = profile.emergencyContactPhone;
    final name = profile.emergencyContactName;

    if (phone == null) {
      return Card(
        child: InkWell(
          onTap: () => openMyDetails(context, context.read<ProfileController>()),
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Row(
              children: [
                CircleAvatar(
                  radius: 23,
                  backgroundColor: accents[1].$1,
                  child: Icon(Icons.contact_phone_outlined, color: accents[1].$2, size: 21),
                ),
                const SizedBox(width: 14),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text('Emergency/guardian contact', style: theme.textTheme.titleSmall),
                      Text(
                        'Not added yet',
                        style: theme.textTheme.bodyMedium?.copyWith(color: scheme.onSurfaceVariant),
                      ),
                    ],
                  ),
                ),
                Icon(Icons.add_circle_outline, color: scheme.primary),
              ],
            ),
          ),
        ),
      );
    }

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Row(
          children: [
            CircleAvatar(
              radius: 23,
              backgroundColor: accents[1].$1,
              child: Text(
                initialsOf(name ?? 'Contact'),
                style: theme.textTheme.titleSmall?.copyWith(color: accents[1].$2),
              ),
            ),
            const SizedBox(width: 14),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    name ?? 'Contact',
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: theme.textTheme.titleSmall?.copyWith(fontWeight: FontWeight.w700),
                  ),
                  Text(
                    phone,
                    style: theme.textTheme.bodyMedium?.copyWith(color: scheme.onSurfaceVariant),
                  ),
                ],
              ),
            ),
            IconButton.filledTonal(
              tooltip: 'Call ${name ?? 'contact'}',
              onPressed: () => callNumber(context, phone),
              style: IconButton.styleFrom(
                backgroundColor: accents[0].$1,
                foregroundColor: accents[0].$2,
              ),
              icon: const Icon(Icons.call_rounded, size: 21),
            ),
          ],
        ),
      ),
    );
  }
}
