import 'package:flutter/material.dart';
import 'package:flutter_animate/flutter_animate.dart';
import 'package:provider/provider.dart';

import '../../../core/theme/app_theme.dart';
import '../../../core/utils/friendly_date.dart';
import '../../../core/widgets/async_view.dart';
import '../../../services/api_client/care_lanka_api.dart';
import '../../../services/api_client/models/appointment_status.dart';
import '../../../services/api_client/models/my_appointment.dart';
import '../services/patient_service.dart';
import '../state/appointments_controller.dart';
import '../state/my_stay_controller.dart';
import '../state/profile_controller.dart';
import '../widgets/panels.dart';
import '../widgets/status_presentation.dart';
import 'bill_sheet.dart';
import 'book_appointment_sheet.dart';
import 'my_details_screen.dart';
import 'past_visits_screen.dart';

class AppointmentsScreen extends StatefulWidget {
  const AppointmentsScreen({super.key});

  @override
  State<AppointmentsScreen> createState() => AppointmentsScreenState();
}

class AppointmentsScreenState extends State<AppointmentsScreen> {
  Future<void> book() async {
    final controller = context.read<AppointmentsController>();
    final request = await showBookAppointmentSheet(context);
    if (request == null || !mounted) return;

    final error = await controller.book(
      scheduledAt: request.scheduledAt,
      reason: request.reason,
    );

    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(
      content: Text(error?.message ?? 'Your visit has been booked.'),
    ));
  }

  Future<void> _cancel(MyAppointment appointment) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Cancel this visit?'),
        content: Text(
          'Your booking for ${FriendlyDate.full(appointment.scheduledAt)} will '
          'be cancelled. You may book another afterwards.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(false),
            child: const Text('Keep booking'),
          ),
          FilledButton(
            onPressed: () => Navigator.of(dialogContext).pop(true),
            style: FilledButton.styleFrom(minimumSize: const Size(0, 44)),
            child: const Text('Cancel visit'),
          ),
        ],
      ),
    );

    if (confirmed != true || !mounted) return;

    final controller = context.read<AppointmentsController>();
    final error = await controller.cancel(appointment.appointmentId);

    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(
      content: Text(error?.message ?? 'Your visit has been cancelled.'),
    ));
  }

  @override
  Widget build(BuildContext context) {
    final controller = context.watch<AppointmentsController>();
    // An unlinked account also gets an empty page from the API, not an error — check isLinked, not list emptiness.
    final linked = context.watch<ProfileController>().isLinked;
    // A patient who is still in a bed books nothing: the ward is already looking after them,
    // and the API refuses it anyway. Hide the button rather than let them meet a 409.
    final admitted = context.watch<MyStayController>().state.valueOrNull is MyStayCurrent;
    Future<void> refresh() => controller.load(showLoading: false);

    final theme = Theme.of(context);
    final scheme = theme.colorScheme;
    
    final hasContent = linked && (controller.upcoming.isNotEmpty || controller.past.isNotEmpty || admitted);

    return Scaffold(
      floatingActionButton: hasContent && !admitted
          ? FloatingActionButton.extended(
              onPressed: controller.busy ? null : book,
              icon: const Icon(Icons.add),
              label: const Text('Book visit'),
              backgroundColor: scheme.primary,
              foregroundColor: scheme.onPrimary,
              elevation: 4,
            ).animate().scale(delay: 400.ms, curve: Curves.easeOutBack)
          : null,
      body: CustomScrollView(
        slivers: [
          SliverToBoxAdapter(
            child: SafeArea(
              bottom: false,
              child: Padding(
                padding: const EdgeInsets.fromLTRB(AppTheme.gutter, 24, AppTheme.gutter, 16),
                child: Text(
                  'Appointments',
                  style: theme.textTheme.headlineMedium?.copyWith(fontWeight: FontWeight.w800),
                ).animate().fadeIn().slideY(begin: -0.2),
              ),
            ),
          ),
          SliverFillRemaining(
            hasScrollBody: false,
            child: AsyncView<List<MyAppointment>>(
              state: controller.appointments,
              onRetry: controller.load,
              loading: const _ListSkeleton(),
              builder: (context, _) {
                final upcoming = controller.upcoming;
                final past = controller.past;

                if (!linked) {
                  return RefreshableMessage(
                    onRefresh: refresh,
                    child: EmptyView(
                      icon: Icons.badge_outlined,
                      title: 'Complete your details',
                      message: 'Add your details before booking a visit.',
                      action: FilledButton(
                        onPressed: () => openMyDetails(context, context.read<ProfileController>()),
                        style: FilledButton.styleFrom(minimumSize: const Size(200, 48)),
                        child: const Text('Add my details'),
                      ),
                    ),
                  );
                }

                if (upcoming.isEmpty && past.isEmpty) {
                  return RefreshableMessage(
                    onRefresh: refresh,
                    child: admitted
                        ? const EmptyView(
                            icon: Icons.local_hospital_rounded,
                            title: 'You are in hospital',
                            message: 'Booking opens again once you have been discharged. Until then '
                                'the ward is looking after everything.',
                          )
                        : EmptyView(
                            icon: Icons.event_available_rounded,
                            title: 'No visits booked',
                            message: 'Your booked visits will appear here.',
                            action: FilledButton.icon(
                              onPressed: controller.busy ? null : book,
                              icon: const Icon(Icons.add),
                              label: const Text('Book a visit'),
                              style: FilledButton.styleFrom(minimumSize: const Size(200, 48)),
                            ),
                          ),
                  );
                }

                return RefreshIndicator(
                  onRefresh: refresh,
                  child: ListView(
                    padding: const EdgeInsets.fromLTRB(AppTheme.gutter, 4, AppTheme.gutter, 104),
                    physics: const NeverScrollableScrollPhysics(),
                    shrinkWrap: true,
                    children: [
                      if (admitted) ...[
                        const _AdmittedNotice().animate().fadeIn(duration: 400.ms).slideY(begin: 0.1),
                        const SizedBox(height: 14),
                      ],
                      if (upcoming.isNotEmpty) ...[
                        const _SectionHeading('Upcoming').animate().fadeIn(),
                        for (var i = 0; i < upcoming.length; i++)
                          Padding(
                            padding: const EdgeInsets.only(bottom: 12),
                            child: _AppointmentCard(
                              appointment: upcoming[i],
                              onCancel: controller.busy ? null : () => _cancel(upcoming[i]),
                            ).animate().fadeIn(delay: (50 * i).ms).slideY(begin: 0.1),
                          ),
                      ],
                      if (past.isNotEmpty) ...[
                        const _SectionHeading('Past').animate().fadeIn(),
                        for (var i = 0; i < past.length; i++)
                          Padding(
                            padding: const EdgeInsets.only(bottom: 12),
                            child: _AppointmentCard(
                              appointment: past[i],
                              onCancel: null,
                            ).animate().fadeIn(delay: (50 * i).ms).slideY(begin: 0.1),
                          ),
                      ],
                    ],
                  ),
                );
              },
            ),
          ),
        ],
      ),
    );
  }
}

class _AdmittedNotice extends StatelessWidget {
  const _AdmittedNotice();

  @override
  Widget build(BuildContext context) {
    return NoticeBanner(
      icon: Icons.local_hospital_rounded,
      accent: Theme.of(context).colorScheme.primary,
      title: 'You are in hospital right now',
      body: 'You cannot book another visit until you have been discharged. '
          'Anything you need while you are here, ask the ward.',
    );
  }
}

class _SectionHeading extends StatelessWidget {
  const _SectionHeading(this.label);

  final String label;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(2, 20, 0, 12),
      child: Row(
        children: [
          Container(
            width: 8,
            height: 8,
            decoration: BoxDecoration(
              color: Theme.of(context).colorScheme.primary,
              shape: BoxShape.circle,
            ),
          ),
          const SizedBox(width: 8),
          Text(
            label,
            style: Theme.of(context).textTheme.titleSmall?.copyWith(
              fontWeight: FontWeight.w800,
              letterSpacing: 0.5,
            ),
          ),
        ],
      ),
    );
  }
}

class _AppointmentCard extends StatelessWidget {
  const _AppointmentCard({required this.appointment, required this.onCancel});

  final MyAppointment appointment;
  final VoidCallback? onCancel;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;
    final isPast = onCancel == null;

    final look = StatusLook.ofAppointment(appointment.status, scheme);
    final accent = isPast ? scheme.outlineVariant : look.color;

    return Container(
      decoration: BoxDecoration(
        color: scheme.surface,
        borderRadius: BorderRadius.circular(AppTheme.radiusL),
        border: Border.all(color: scheme.outlineVariant.withValues(alpha: 0.15)),
        boxShadow: [
          BoxShadow(
            color: scheme.shadow.withValues(alpha: 0.05),
            blurRadius: 10,
            offset: const Offset(0, 4),
          ),
        ],
      ),
      child: IntrinsicHeight(
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Container(
              width: 6,
              decoration: BoxDecoration(
                color: accent,
                borderRadius: const BorderRadius.horizontal(
                  left: Radius.circular(AppTheme.radiusL),
                ),
              ),
            ),
            Expanded(
              child: Padding(
                padding: const EdgeInsets.all(16),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        DateBlock(date: appointment.scheduledAt, muted: isPast),
                        const SizedBox(width: 16),
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Row(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                                children: [
                                  Expanded(
                                    child: Text(
                                      FriendlyDate.time(appointment.scheduledAt),
                                      style: theme.textTheme.titleMedium?.copyWith(
                                        fontWeight: FontWeight.w800,
                                      ),
                                    ),
                                  ),
                                  StatusChip.appointment(
                                    status: appointment.status,
                                    label: appointment.statusText,
                                    scheme: scheme,
                                    compact: true,
                                  ),
                                ],
                              ),
                              const SizedBox(height: 4),
                              Text(
                                isPast
                                    ? FriendlyDate.dayAndMonth(appointment.scheduledAt)
                                    : FriendlyDate.countdown(appointment.scheduledAt),
                                style: theme.textTheme.bodySmall?.copyWith(
                                  color: scheme.onSurfaceVariant,
                                  fontWeight: FontWeight.w500,
                                ),
                              ),
                            ],
                          ),
                        ),
                      ],
                    ),
                    if (appointment.reason != null) ...[
                      const SizedBox(height: 16),
                      Container(
                        width: double.infinity,
                        padding: const EdgeInsets.all(12),
                        decoration: BoxDecoration(
                          color: scheme.surfaceContainerHighest.withValues(alpha: 0.3),
                          borderRadius: BorderRadius.circular(AppTheme.radiusS),
                          border: Border.all(color: scheme.outlineVariant.withValues(alpha: 0.2)),
                        ),
                        child: Text(
                          appointment.reason!,
                          style: theme.textTheme.bodyMedium?.copyWith(height: 1.4),
                        ),
                      ),
                    ],
                    if (appointment.cancellationReason != null) ...[
                      const SizedBox(height: 16),
                      NoticeBanner(
                        icon: Icons.info_outline,
                        accent: scheme.error,
                        title: 'Cancelled by the hospital',
                        body: appointment.cancellationReason!,
                      ),
                    ],
                    if ((appointment.canCancel && onCancel != null) || appointment.status == AppointmentStatus.completed) ...[
                      const SizedBox(height: 16),
                      const Divider(),
                      const SizedBox(height: 8),
                      Row(
                        mainAxisAlignment: MainAxisAlignment.end,
                        children: [
                          if (appointment.status == AppointmentStatus.completed)
                            Expanded(
                              child: OutlinedButton.icon(
                                onPressed: () => showAppointmentBillSheet(
                                  context,
                                  service: PatientService(context.read<CareLankaApi>()),
                                  appointmentId: appointment.appointmentId,
                                  onViewPastVisits: () => Navigator.of(context).push(
                                    MaterialPageRoute(builder: (_) => const PastVisitsScreen()),
                                  ),
                                ),
                                icon: const Icon(Icons.receipt_long_rounded, size: 18),
                                label: const Text('View Bill'),
                                style: OutlinedButton.styleFrom(
                                  minimumSize: const Size.fromHeight(44),
                                  shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(999)),
                                ),
                              ),
                            ),
                          if (appointment.canCancel && onCancel != null) ...[
                            if (appointment.status == AppointmentStatus.completed)
                              const SizedBox(width: 12),
                            TextButton.icon(
                              onPressed: onCancel,
                              icon: const Icon(Icons.close_rounded, size: 18),
                              label: const Text('Cancel'),
                              style: TextButton.styleFrom(
                                foregroundColor: scheme.error,
                                shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(999)),
                              ),
                            ),
                          ],
                        ],
                      ),
                    ],
                  ],
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _ListSkeleton extends StatelessWidget {
  const _ListSkeleton();

  @override
  Widget build(BuildContext context) {
    return ListView(
      padding: const EdgeInsets.fromLTRB(AppTheme.gutter, 24, AppTheme.gutter, 32),
      children: const [
        Skeleton(height: 20, width: 90),
        SizedBox(height: 16),
        Skeleton(height: 150, radius: AppTheme.radiusL),
        SizedBox(height: 12),
        Skeleton(height: 150, radius: AppTheme.radiusL),
      ],
    );
  }
}
