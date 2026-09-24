import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../../core/theme/app_theme.dart';
import '../../../core/utils/friendly_date.dart';
import '../../../core/widgets/async_data.dart';
import '../../../core/widgets/async_view.dart';
import '../../../services/api_client/models/my_admission.dart';
import '../../../services/api_client/models/my_appointment.dart';
import '../../../services/api_client/models/my_profile.dart';
import '../../emergency/emergency_routes.dart';
import '../state/appointments_controller.dart';
import '../state/my_stay_controller.dart';
import '../state/profile_controller.dart';
import '../widgets/dialer.dart';
import '../widgets/panels.dart';
import '../widgets/patient_id_card.dart';
import 'claim_record_screen.dart';
import 'my_details_screen.dart';
import 'my_reports_screen.dart';
import 'past_visits_screen.dart';

class HomeScreen extends StatelessWidget {
  const HomeScreen({super.key, required this.onOpenTab});

  final void Function(PatientTab tab) onOpenTab;

  @override
  Widget build(BuildContext context) {
    final profile = context.watch<ProfileController>().profile.valueOrNull;
    final stay = context.watch<MyStayController>();
    final appointments = context.watch<AppointmentsController>();

    return Scaffold(
      body: RefreshIndicator(
        onRefresh: () async {
          await Future.wait([
            context.read<ProfileController>().load(showLoading: false),
            stay.load(showLoading: false),
            appointments.load(showLoading: false),
          ]);
        },
        child: ListView(
          padding: const EdgeInsets.fromLTRB(
            AppTheme.gutter,
            0,
            AppTheme.gutter,
            32,
          ),
          children: profile == null
              ? const [_NotLinkedYet()]
              : [
                  _Greeting(profile: profile),
                  const SizedBox(height: 20),
                  _WhatsNext(
                    stay: stay.state,
                    nextVisit: appointments.upcoming.isEmpty
                        ? null
                        : appointments.upcoming.first,
                    onOpenTab: onOpenTab,
                  ),
                  const SizedBox(height: 16),
                  PatientIdCard(
                    patientCode: profile.patientCode,
                    fullName: profile.fullName,
                  ),
                  if (!profile.detailsComplete) ...[
                    const SizedBox(height: 16),
                    _CompleteDetailsBanner(missing: profile.missingFields),
                  ],
                  const SizedBox(height: 24),
                  Text(
                    'Quick actions',
                    style: Theme.of(context).textTheme.titleSmall,
                  ),
                  const SizedBox(height: 12),
                  _QuickActions(
                    profile: profile,
                    onOpenTab: onOpenTab,
                    admitted: stay.state.valueOrNull is MyStayCurrent,
                  ),
                ],
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
        Text(_timeOfDayGreeting(), style: theme.textTheme.headlineSmall),
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
                onPressed: () =>
                    openMyDetails(context, context.read<ProfileController>()),
                style: FilledButton.styleFrom(minimumSize: const Size(0, 44)),
                child: const Text('Add my details'),
              ),
              OutlinedButton(
                onPressed: () =>
                    openClaimRecord(context, context.read<ProfileController>()),
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
  const _Greeting({required this.profile});

  final MyProfile profile;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Row(
      children: [
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                _timeOfDayGreeting(),
                style: theme.textTheme.bodyMedium?.copyWith(
                  color: theme.colorScheme.onSurfaceVariant,
                ),
              ),
              const SizedBox(height: 2),
              Text(
                _firstName(profile.fullName),
                style: theme.textTheme.headlineSmall,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
              ),
            ],
          ),
        ),
        CircleAvatar(
          radius: 22,
          backgroundColor: theme.colorScheme.primaryContainer,
          child: Text(
            initialsOf(profile.fullName),
            style: theme.textTheme.titleMedium?.copyWith(
              color: theme.colorScheme.onPrimaryContainer,
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
  const _WhatsNext({
    required this.stay,
    required this.nextVisit,
    required this.onOpenTab,
  });

  final AsyncData<MyStay> stay;
  final MyAppointment? nextVisit;
  final void Function(PatientTab tab) onOpenTab;

  @override
  Widget build(BuildContext context) {
    return switch (stay) {
      AsyncLoading<MyStay>() => const Skeleton.card(height: 150),
      AsyncFailed<MyStay>() => _NextVisitOrNothing(
        nextVisit: nextVisit,
        onOpenTab: onOpenTab,
      ),
      AsyncReady<MyStay>(value: MyStayCurrent(:final admission)) =>
        _CurrentStayCard(admission: admission, onOpenTab: onOpenTab),
      AsyncReady<MyStay>() => _NextVisitOrNothing(
        nextVisit: nextVisit,
        onOpenTab: onOpenTab,
      ),
    };
  }
}

class _CurrentStayCard extends StatelessWidget {
  const _CurrentStayCard({required this.admission, required this.onOpenTab});

  final MyAdmission admission;
  final void Function(PatientTab tab) onOpenTab;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final place = [
      admission.wardName,
      admission.bedNumber,
    ].whereType<String>().join(' · ');

    return _HeroCard(
      onTap: () => onOpenTab(PatientTab.myStay),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Text(
                'YOUR STAY',
                style: theme.textTheme.labelSmall?.copyWith(
                  color: Colors.white.withValues(alpha: 0.75),
                  letterSpacing: 1,
                ),
              ),
              const Spacer(),
              Icon(
                Icons.chevron_right,
                color: Colors.white.withValues(alpha: 0.8),
                size: 20,
              ),
            ],
          ),
          const SizedBox(height: 10),
          Text(
            admission.statusText,
            style: theme.textTheme.headlineSmall?.copyWith(color: Colors.white),
          ),
          if (place.isNotEmpty) ...[
            const SizedBox(height: 8),
            Row(
              children: [
                Icon(
                  Icons.place_outlined,
                  size: 16,
                  color: Colors.white.withValues(alpha: 0.85),
                ),
                const SizedBox(width: 6),
                Text(
                  place,
                  style: theme.textTheme.bodyMedium?.copyWith(
                    color: Colors.white.withValues(alpha: 0.9),
                  ),
                ),
              ],
            ),
          ],
          if (!admission.detailsComplete) ...[
            const SizedBox(height: 12),
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
              decoration: BoxDecoration(
                color: Colors.white.withValues(alpha: 0.18),
                borderRadius: BorderRadius.circular(999),
              ),
              child: Text(
                '${admission.missingFields.length} detail'
                '${admission.missingFields.length == 1 ? '' : 's'} missing',
                style: theme.textTheme.labelSmall?.copyWith(
                  color: Colors.white,
                ),
              ),
            ),
          ],
          const SizedBox(height: 12),
          Text(
            'View your progress',
            style: theme.textTheme.bodySmall?.copyWith(
              color: Colors.white.withValues(alpha: 0.75),
            ),
          ),
        ],
      ),
    );
  }
}

class _NextVisitOrNothing extends StatelessWidget {
  const _NextVisitOrNothing({required this.nextVisit, required this.onOpenTab});

  final MyAppointment? nextVisit;
  final void Function(PatientTab tab) onOpenTab;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final visit = nextVisit;

    if (visit == null) {
      return Card(
        child: Padding(
          padding: const EdgeInsets.all(22),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Icon(
                Icons.event_available_outlined,
                size: 26,
                color: theme.colorScheme.primary,
              ),
              const SizedBox(height: 12),
              Text('No upcoming visits', style: theme.textTheme.titleMedium),
              const SizedBox(height: 4),
              Text(
                'Book a visit when you need to be seen.',
                style: theme.textTheme.bodySmall?.copyWith(
                  color: theme.colorScheme.onSurfaceVariant,
                ),
              ),
              const SizedBox(height: 16),
              FilledButton.icon(
                onPressed: () => onOpenTab(PatientTab.appointments),
                icon: const Icon(Icons.add),
                label: const Text('Book a visit'),
              ),
            ],
          ),
        ),
      );
    }

    return _HeroCard(
      onTap: () => onOpenTab(PatientTab.appointments),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Text(
                'YOUR NEXT VISIT',
                style: theme.textTheme.labelSmall?.copyWith(
                  color: Colors.white.withValues(alpha: 0.75),
                  letterSpacing: 1,
                ),
              ),
              const Spacer(),
              Icon(
                Icons.chevron_right,
                color: Colors.white.withValues(alpha: 0.8),
                size: 20,
              ),
            ],
          ),
          const SizedBox(height: 10),
          Text(
            FriendlyDate.relativeDayAndTime(visit.scheduledAt),
            style: theme.textTheme.headlineSmall?.copyWith(color: Colors.white),
          ),
          const SizedBox(height: 8),
          Row(
            children: [
              Container(
                padding: const EdgeInsets.symmetric(
                  horizontal: 10,
                  vertical: 5,
                ),
                decoration: BoxDecoration(
                  color: Colors.white.withValues(alpha: 0.18),
                  borderRadius: BorderRadius.circular(999),
                ),
                child: Text(
                  FriendlyDate.countdown(visit.scheduledAt),
                  style: theme.textTheme.labelSmall?.copyWith(
                    color: Colors.white,
                    fontWeight: FontWeight.w700,
                  ),
                ),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: Text(
                  visit.statusText,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: theme.textTheme.bodySmall?.copyWith(
                    color: Colors.white.withValues(alpha: 0.85),
                  ),
                ),
              ),
            ],
          ),
          if (visit.reason != null) ...[
            const SizedBox(height: 12),
            Text(
              visit.reason!,
              maxLines: 2,
              overflow: TextOverflow.ellipsis,
              style: theme.textTheme.bodyMedium?.copyWith(
                color: Colors.white.withValues(alpha: 0.9),
              ),
            ),
          ],
        ],
      ),
    );
  }
}

class _HeroCard extends StatelessWidget {
  const _HeroCard({required this.child, required this.onTap});

  final Widget child;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;

    return Material(
      color: Colors.transparent,
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(AppTheme.radiusL),
        child: Ink(
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(AppTheme.radiusL),
            gradient: LinearGradient(
              begin: Alignment.topLeft,
              end: Alignment.bottomRight,
              colors: [
                scheme.primary,
                Color.lerp(scheme.primary, Colors.black, 0.3)!,
              ],
            ),
          ),
          child: Padding(padding: const EdgeInsets.all(20), child: child),
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
          minimumSize: const Size(0, 42),
          padding: const EdgeInsets.symmetric(horizontal: 18),
        ),
        child: const Text('Add them now'),
      ),
    );
  }
}

class _QuickActions extends StatelessWidget {
  const _QuickActions({
    required this.profile,
    required this.onOpenTab,
    required this.admitted,
  });

  final MyProfile profile;
  final void Function(PatientTab tab) onOpenTab;

  // While admitted, My stay already shows the current stay, not past ones — the tile here
  // would be redundant. Past visits stays reachable from Profile regardless.
  final bool admitted;

  @override
  Widget build(BuildContext context) {
    final emergencyPhone = profile.emergencyContactPhone;

    return GridView.count(
      crossAxisCount: 2,
      shrinkWrap: true,
      physics: const NeverScrollableScrollPhysics(),
      mainAxisSpacing: 12,
      crossAxisSpacing: 12,
      childAspectRatio: 1.55,
      children: [
        _ActionTile(
          icon: Icons.emergency_outlined,
          label: 'Request ambulance',
          caption: 'Share your location',
          tone: _ActionTone.urgent,
          onTap: () => context.push(EmergencyPaths.patientReport),
        ),
        _ActionTile(
          icon: Icons.add_circle_outline,
          label: 'Book a visit',
          caption: 'Choose a date and time',
          onTap: () => onOpenTab(PatientTab.appointments),
        ),
        _ActionTile(
          icon: Icons.monitor_heart_outlined,
          label: 'My stay',
          caption: 'Current admission',
          onTap: () => onOpenTab(PatientTab.myStay),
        ),
        if (!admitted)
          _ActionTile(
            icon: Icons.history,
            label: 'Past visits',
            caption: 'Completed stays',
            onTap: () => openPastVisits(context),
          ),
        _ActionTile(
          icon: Icons.description_outlined,
          label: 'My reports',
          caption: 'Lab results',
          onTap: () => Navigator.of(
            context,
          ).push(MaterialPageRoute(builder: (_) => const MyReportsScreen())),
        ),
        if (emergencyPhone != null)
          _ActionTile(
            icon: Icons.phone_in_talk_outlined,
            label: 'Call ${profile.emergencyContactName ?? 'contact'}',
            caption: emergencyPhone,
            tone: _ActionTone.urgent,
            onTap: () => callNumber(context, emergencyPhone),
          )
        else
          _ActionTile(
            icon: Icons.contact_phone_outlined,
            label: 'Emergency/guardian contact',
            caption: 'Not added yet',
            onTap: () =>
                openMyDetails(context, context.read<ProfileController>()),
          ),
      ],
    );
  }
}

enum _ActionTone { normal, urgent }

class _ActionTile extends StatelessWidget {
  const _ActionTile({
    required this.icon,
    required this.label,
    required this.caption,
    required this.onTap,
    this.tone = _ActionTone.normal,
  });

  final IconData icon;
  final String label;
  final String caption;
  final VoidCallback onTap;
  final _ActionTone tone;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;
    final urgent = tone == _ActionTone.urgent;

    return Card(
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(AppTheme.radiusL),
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Container(
                padding: const EdgeInsets.all(8),
                decoration: BoxDecoration(
                  color: urgent
                      ? scheme.errorContainer
                      : scheme.primaryContainer,
                  borderRadius: BorderRadius.circular(AppTheme.radiusS),
                ),
                child: Icon(
                  icon,
                  size: 18,
                  color: urgent
                      ? scheme.onErrorContainer
                      : scheme.onPrimaryContainer,
                ),
              ),
              Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    label,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: theme.textTheme.titleSmall,
                  ),
                  const SizedBox(height: 1),
                  Text(
                    caption,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: theme.textTheme.bodySmall?.copyWith(
                      color: scheme.onSurfaceVariant,
                    ),
                  ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }
}
