import 'package:flutter/material.dart';
import 'package:flutter_animate/flutter_animate.dart';
import 'package:provider/provider.dart';

import '../../../core/theme/app_theme.dart';
import '../../../core/utils/friendly_date.dart';
import '../../../core/widgets/async_data.dart';
import '../../../core/widgets/async_view.dart';
import '../../../services/api_client/models/my_admission.dart';
import '../../../services/api_client/models/my_appointment.dart';
import '../../../services/api_client/models/my_profile.dart';
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
    final theme = Theme.of(context);

    return Scaffold(
      backgroundColor: theme.scaffoldBackgroundColor,
      body: RefreshIndicator(
        onRefresh: () async {
          await Future.wait([
            context.read<ProfileController>().load(showLoading: false),
            stay.load(showLoading: false),
            appointments.load(showLoading: false),
          ]);
        },
        child: CustomScrollView(
          slivers: [
            SliverAppBar(
              floating: true,
              backgroundColor: theme.scaffoldBackgroundColor.withValues(alpha: 0.9),
              surfaceTintColor: Colors.transparent,
              elevation: 0,
              expandedHeight: 20,
            ),
            SliverPadding(
              padding: const EdgeInsets.fromLTRB(AppTheme.gutter, 0, AppTheme.gutter, 40),
              sliver: SliverList(
                delegate: SliverChildListDelegate([
                  if (profile == null)
                    const _NotLinkedYet().animate().fadeIn(duration: 400.ms).slideY(begin: 0.1)
                  else ...[
                    _Greeting(profile: profile).animate().fadeIn(duration: 400.ms).slideY(begin: 0.1),
                    const SizedBox(height: 24),
                    _WhatsNext(
                      stay: stay.state,
                      nextVisit: appointments.upcoming.isEmpty ? null : appointments.upcoming.first,
                      onOpenTab: onOpenTab,
                    ).animate().fadeIn(delay: 100.ms, duration: 400.ms).slideY(begin: 0.1),
                    const SizedBox(height: 24),
                    PatientIdCard(
                      patientCode: profile.patientCode,
                      fullName: profile.fullName,
                    ).animate().fadeIn(delay: 200.ms, duration: 400.ms).slideY(begin: 0.1),
                    if (!profile.detailsComplete) ...[
                      const SizedBox(height: 16),
                      _CompleteDetailsBanner(missing: profile.missingFields)
                          .animate().fadeIn(delay: 300.ms, duration: 400.ms).scale(begin: const Offset(0.95, 0.95)),
                    ],
                    const SizedBox(height: 32),
                    Text(
                      'Quick Actions',
                      style: theme.textTheme.titleMedium?.copyWith(
                        color: theme.colorScheme.onSurface.withValues(alpha: 0.8),
                        letterSpacing: 0.5,
                      ),
                    ).animate().fadeIn(delay: 400.ms, duration: 400.ms),
                    const SizedBox(height: 16),
                    _QuickActions(profile: profile, onOpenTab: onOpenTab)
                        .animate().fadeIn(delay: 500.ms, duration: 400.ms).slideY(begin: 0.1),
                  ],
                ]),
              ),
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
        Text(_timeOfDayGreeting(), style: theme.textTheme.headlineMedium?.copyWith(fontWeight: FontWeight.w800)),
        const SizedBox(height: 24),
        NoticeBanner(
          icon: Icons.badge_outlined,
          accent: theme.colorScheme.primary,
          title: 'Complete your registration',
          body:
              'Add your details to book visits and view your stay. If the hospital has '
              'already registered you at the desk, use your patient code instead so your '
              'stay and history come with you.',
          action: Wrap(
            spacing: 12,
            runSpacing: 12,
            children: [
              FilledButton(
                onPressed: () => openMyDetails(context, context.read<ProfileController>()),
                style: FilledButton.styleFrom(minimumSize: const Size(0, 48)),
                child: const Text('Add my details'),
              ),
              OutlinedButton(
                onPressed: () => openClaimRecord(context, context.read<ProfileController>()),
                style: OutlinedButton.styleFrom(minimumSize: const Size(0, 48)),
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
                style: theme.textTheme.titleMedium?.copyWith(
                  color: theme.colorScheme.onSurfaceVariant,
                  fontWeight: FontWeight.w500,
                ),
              ),
              const SizedBox(height: 4),
              Text(
                _firstName(profile.fullName),
                style: theme.textTheme.headlineMedium?.copyWith(
                  fontWeight: FontWeight.w800,
                  letterSpacing: -0.5,
                  color: theme.colorScheme.onSurface,
                ),
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
              ),
            ],
          ),
        ),
        Container(
          decoration: BoxDecoration(
            shape: BoxShape.circle,
            boxShadow: [
              BoxShadow(
                color: theme.colorScheme.primary.withValues(alpha: 0.2),
                blurRadius: 12,
                offset: const Offset(0, 4),
              ),
            ],
          ),
          child: CircleAvatar(
            radius: 26,
            backgroundColor: theme.colorScheme.primary,
            child: Text(
              initialsOf(profile.fullName),
              style: theme.textTheme.titleLarge?.copyWith(
                color: theme.colorScheme.onPrimary,
                fontWeight: FontWeight.w700,
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
  if (hour < 12) return 'Good morning,';
  if (hour < 17) return 'Good afternoon,';
  return 'Good evening,';
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
      AsyncLoading<MyStay>() => const Skeleton.card(height: 180),
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
      gradientColors: [
        theme.colorScheme.primary,
        theme.colorScheme.tertiary, // A subtle shift in color
      ],
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                decoration: BoxDecoration(
                  color: Colors.white.withValues(alpha: 0.2),
                  borderRadius: BorderRadius.circular(6),
                ),
                child: Text(
                  'YOUR STAY',
                  style: theme.textTheme.labelSmall?.copyWith(
                    color: Colors.white,
                    letterSpacing: 1.2,
                    fontWeight: FontWeight.w800,
                  ),
                ),
              ),
              const Spacer(),
              Container(
                padding: const EdgeInsets.all(4),
                decoration: BoxDecoration(
                  shape: BoxShape.circle,
                  color: Colors.white.withValues(alpha: 0.15),
                ),
                child: const Icon(
                  Icons.arrow_forward_ios_rounded,
                  color: Colors.white,
                  size: 14,
                ),
              ),
            ],
          ),
          const SizedBox(height: 16),
          Text(
            admission.statusText,
            style: theme.textTheme.headlineSmall?.copyWith(
              color: Colors.white,
              fontWeight: FontWeight.w700,
            ),
          ),
          if (place.isNotEmpty) ...[
            const SizedBox(height: 8),
            Row(
              children: [
                Icon(
                  Icons.place_rounded,
                  size: 18,
                  color: Colors.white.withValues(alpha: 0.9),
                ),
                const SizedBox(width: 8),
                Text(
                  place,
                  style: theme.textTheme.titleMedium?.copyWith(
                    color: Colors.white,
                  ),
                ),
              ],
            ),
          ],
          if (!admission.detailsComplete) ...[
            const SizedBox(height: 16),
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
              decoration: BoxDecoration(
                color: theme.colorScheme.errorContainer.withValues(alpha: 0.9),
                borderRadius: BorderRadius.circular(AppTheme.radiusM),
              ),
              child: Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Icon(Icons.warning_amber_rounded, size: 16, color: theme.colorScheme.onErrorContainer),
                  const SizedBox(width: 6),
                  Text(
                    '${admission.missingFields.length} detail${admission.missingFields.length == 1 ? '' : 's'} missing',
                    style: theme.textTheme.labelMedium?.copyWith(
                      color: theme.colorScheme.onErrorContainer,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                ],
              ),
            ),
          ],
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
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(AppTheme.radiusL),
          side: BorderSide(color: theme.colorScheme.primary.withValues(alpha: 0.1), width: 1.5),
        ),
        color: theme.colorScheme.primaryContainer.withValues(alpha: 0.3),
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Container(
                padding: const EdgeInsets.all(12),
                decoration: BoxDecoration(
                  color: theme.colorScheme.primary.withValues(alpha: 0.1),
                  shape: BoxShape.circle,
                ),
                child: Icon(
                  Icons.calendar_month_rounded,
                  size: 28,
                  color: theme.colorScheme.primary,
                ),
              ),
              const SizedBox(height: 16),
              Text('No upcoming visits', style: theme.textTheme.titleLarge),
              const SizedBox(height: 6),
              Text(
                'Book a visit when you need to be seen by a doctor.',
                style: theme.textTheme.bodyMedium?.copyWith(
                  color: theme.colorScheme.onSurfaceVariant,
                ),
              ),
              const SizedBox(height: 20),
              FilledButton.icon(
                onPressed: () => onOpenTab(PatientTab.appointments),
                icon: const Icon(Icons.add_rounded),
                label: const Text('Book a visit'),
                style: FilledButton.styleFrom(
                  elevation: 0,
                ),
              ),
            ],
          ),
        ),
      );
    }

    return _HeroCard(
      onTap: () => onOpenTab(PatientTab.appointments),
      gradientColors: [
        const Color(0xFF0F172A),
        const Color(0xFF1E293B),
      ],
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                decoration: BoxDecoration(
                  color: Colors.white.withValues(alpha: 0.15),
                  borderRadius: BorderRadius.circular(6),
                ),
                child: Text(
                  'YOUR NEXT VISIT',
                  style: theme.textTheme.labelSmall?.copyWith(
                    color: Colors.white,
                    letterSpacing: 1.2,
                    fontWeight: FontWeight.w800,
                  ),
                ),
              ),
              const Spacer(),
              Container(
                padding: const EdgeInsets.all(4),
                decoration: BoxDecoration(
                  shape: BoxShape.circle,
                  color: Colors.white.withValues(alpha: 0.1),
                ),
                child: const Icon(
                  Icons.arrow_forward_ios_rounded,
                  color: Colors.white,
                  size: 14,
                ),
              ),
            ],
          ),
          const SizedBox(height: 16),
          Text(
            FriendlyDate.relativeDayAndTime(visit.scheduledAt),
            style: theme.textTheme.headlineSmall?.copyWith(
              color: Colors.white,
              fontWeight: FontWeight.w700,
            ),
          ),
          const SizedBox(height: 12),
          Row(
            children: [
              Container(
                padding: const EdgeInsets.symmetric(
                  horizontal: 12,
                  vertical: 6,
                ),
                decoration: BoxDecoration(
                  color: theme.colorScheme.primary,
                  borderRadius: BorderRadius.circular(AppTheme.radiusM),
                ),
                child: Text(
                  FriendlyDate.countdown(visit.scheduledAt).toUpperCase(),
                  style: theme.textTheme.labelSmall?.copyWith(
                    color: theme.colorScheme.onPrimary,
                    fontWeight: FontWeight.w800,
                    letterSpacing: 0.5,
                  ),
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Text(
                  visit.statusText,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: theme.textTheme.titleSmall?.copyWith(
                    color: Colors.white.withValues(alpha: 0.9),
                  ),
                ),
              ),
            ],
          ),
          if (visit.reason != null) ...[
            const SizedBox(height: 16),
            Container(
              padding: const EdgeInsets.all(12),
              decoration: BoxDecoration(
                color: Colors.white.withValues(alpha: 0.05),
                borderRadius: BorderRadius.circular(AppTheme.radiusM),
              ),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Icon(Icons.notes_rounded, size: 16, color: Colors.white.withValues(alpha: 0.5)),
                  const SizedBox(width: 8),
                  Expanded(
                    child: Text(
                      visit.reason!,
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                      style: theme.textTheme.bodyMedium?.copyWith(
                        color: Colors.white.withValues(alpha: 0.8),
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ],
        ],
      ),
    );
  }
}

class _HeroCard extends StatelessWidget {
  const _HeroCard({required this.child, required this.onTap, required this.gradientColors});

  final Widget child;
  final VoidCallback onTap;
  final List<Color> gradientColors;

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(AppTheme.radiusL),
        boxShadow: [
          BoxShadow(
            color: gradientColors.first.withValues(alpha: 0.25),
            blurRadius: 20,
            offset: const Offset(0, 8),
          ),
        ],
      ),
      child: Material(
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
                colors: gradientColors,
              ),
            ),
            child: Padding(padding: const EdgeInsets.all(24), child: child),
          ),
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
      icon: Icons.info_rounded,
      accent: scheme.error,
      title: 'Incomplete Details',
      body: 'The hospital requires the following before your next visit.',
      bullets: missing.map(prettyFieldName).toList(),
      action: FilledButton(
        onPressed: () => openMyDetails(context, controller),
        style: FilledButton.styleFrom(
          backgroundColor: scheme.error,
          foregroundColor: scheme.onError,
          minimumSize: const Size(0, 48),
          padding: const EdgeInsets.symmetric(horizontal: 24),
        ),
        child: const Text('Add them now'),
      ),
    );
  }
}

class _QuickActions extends StatelessWidget {
  const _QuickActions({required this.profile, required this.onOpenTab});

  final MyProfile profile;
  final void Function(PatientTab tab) onOpenTab;

  @override
  Widget build(BuildContext context) {
    final emergencyPhone = profile.emergencyContactPhone;

    return GridView.count(
      crossAxisCount: 2,
      shrinkWrap: true,
      physics: const NeverScrollableScrollPhysics(),
      mainAxisSpacing: 16,
      crossAxisSpacing: 16,
      childAspectRatio: 1.1, // Taller cards for better tap targets and layout
      children: [
        _ActionTile(
          icon: Icons.add_circle_rounded,
          label: 'Book a visit',
          caption: 'Choose date & time',
          onTap: () => onOpenTab(PatientTab.appointments),
        ),
        _ActionTile(
          icon: Icons.monitor_heart_rounded,
          label: 'My stay',
          caption: 'Current admission',
          onTap: () => onOpenTab(PatientTab.myStay),
        ),
        _ActionTile(
          icon: Icons.history_rounded,
          label: 'Past visits',
          caption: 'Completed stays',
          onTap: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => const PastVisitsScreen())),
        ),
        _ActionTile(
          icon: Icons.description_rounded,
          label: 'My reports',
          caption: 'Lab results',
          onTap: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => const MyReportsScreen())),
        ),
        if (emergencyPhone != null)
          _ActionTile(
            icon: Icons.phone_in_talk_rounded,
            label: 'Call ${profile.emergencyContactName ?? 'contact'}',
            caption: emergencyPhone,
            tone: _ActionTone.urgent,
            onTap: () => callNumber(context, emergencyPhone),
          )
        else
          _ActionTile(
            icon: Icons.contact_phone_rounded,
            label: 'Emergency contact',
            caption: 'Not added yet',
            onTap: () => openMyDetails(context, context.read<ProfileController>()),
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
      elevation: 0,
      color: theme.colorScheme.surface,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(AppTheme.radiusM),
        side: BorderSide(color: scheme.outlineVariant.withValues(alpha: 0.2)),
      ),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(AppTheme.radiusM),
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Container(
                padding: const EdgeInsets.all(10),
                decoration: BoxDecoration(
                  color: urgent
                      ? scheme.errorContainer.withValues(alpha: 0.5)
                      : scheme.primaryContainer.withValues(alpha: 0.5),
                  shape: BoxShape.circle,
                ),
                child: Icon(
                  icon,
                  size: 24,
                  color: urgent
                      ? scheme.error
                      : scheme.primary,
                ),
              ),
              const Spacer(),
              Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    label,
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                    style: theme.textTheme.titleSmall?.copyWith(fontWeight: FontWeight.w700),
                  ),
                  const SizedBox(height: 2),
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
