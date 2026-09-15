import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/theme/app_theme.dart';
import '../../../core/utils/friendly_date.dart';
import '../../../core/widgets/async_view.dart';
import '../../../services/api_client/models/appointment_status.dart';
import '../../../services/api_client/models/my_appointment.dart';
import '../hospital_contact.dart';
import '../state/appointments_controller.dart';
import '../state/profile_controller.dart';
import '../widgets/panels.dart';
import '../widgets/status_presentation.dart';
import 'book_appointment_sheet.dart';
import 'my_details_screen.dart';

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

    return Scaffold(
      appBar: AppBar(title: const Text('Appointments')),
      floatingActionButton: linked
          ? FloatingActionButton.extended(
              onPressed: controller.busy ? null : book,
              icon: const Icon(Icons.add),
              label: const Text('Book a visit'),
            )
          : null,
      body: AsyncView<List<MyAppointment>>(
        state: controller.appointments,
        onRetry: controller.load,
        loading: const _ListSkeleton(),
        builder: (context, _) {
          final upcoming = controller.upcoming;
          final past = controller.past;

          if (!linked) {
            return EmptyView(
              icon: Icons.badge_outlined,
              title: 'Complete your details',
              message: 'Add your details before booking a visit.',
              action: FilledButton(
                onPressed: () => openMyDetails(context, context.read<ProfileController>()),
                style: FilledButton.styleFrom(minimumSize: const Size(200, 48)),
                child: const Text('Add my details'),
              ),
            );
          }

          if (upcoming.isEmpty && past.isEmpty) {
            return EmptyView(
              icon: Icons.event_available_outlined,
              title: 'No visits booked',
              message: 'Your booked visits will appear here.',
              action: FilledButton.icon(
                onPressed: controller.busy ? null : book,
                icon: const Icon(Icons.add),
                label: const Text('Book a visit'),
                style: FilledButton.styleFrom(minimumSize: const Size(200, 48)),
              ),
            );
          }

          return RefreshIndicator(
            onRefresh: controller.load,
            child: ListView(
              padding: const EdgeInsets.fromLTRB(AppTheme.gutter, 4, AppTheme.gutter, 104),
              children: [
                if (upcoming.isNotEmpty) ...[
                  const _SectionHeading('Upcoming'),
                  for (final appointment in upcoming)
                    Padding(
                      padding: const EdgeInsets.only(bottom: 10),
                      child: _AppointmentCard(
                        appointment: appointment,
                        onCancel: controller.busy ? null : () => _cancel(appointment),
                      ),
                    ),
                ],
                if (past.isNotEmpty) ...[
                  const _SectionHeading('Past'),
                  for (final appointment in past)
                    Padding(
                      padding: const EdgeInsets.only(bottom: 10),
                      child: _AppointmentCard(appointment: appointment, onCancel: null),
                    ),
                ],
              ],
            ),
          );
        },
      ),
    );
  }
}

class _SectionHeading extends StatelessWidget {
  const _SectionHeading(this.label);

  final String label;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(2, 18, 0, 10),
      child: Text(label, style: Theme.of(context).textTheme.titleSmall),
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

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                DateBlock(date: appointment.scheduledAt, muted: isPast),
                const SizedBox(width: 14),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        FriendlyDate.time(appointment.scheduledAt),
                        style: theme.textTheme.titleMedium,
                      ),
                      const SizedBox(height: 2),
                      Text(
                        isPast
                            ? FriendlyDate.dayAndMonth(appointment.scheduledAt)
                            : FriendlyDate.countdown(appointment.scheduledAt),
                        style: theme.textTheme.bodySmall
                            ?.copyWith(color: scheme.onSurfaceVariant),
                      ),
                      const SizedBox(height: 10),
                      StatusChip.appointment(
                        status: appointment.status,
                        label: appointment.statusText,
                        scheme: scheme,
                        compact: true,
                      ),
                    ],
                  ),
                ),
              ],
            ),
            if (appointment.reason != null) ...[
              const SizedBox(height: 14),
              Container(
                width: double.infinity,
                padding: const EdgeInsets.all(12),
                decoration: BoxDecoration(
                  color: scheme.surfaceContainerHighest.withValues(alpha: 0.5),
                  borderRadius: BorderRadius.circular(AppTheme.radiusM),
                ),
                child: Text(appointment.reason!, style: theme.textTheme.bodyMedium),
              ),
            ],
            if (appointment.cancellationReason != null) ...[
              const SizedBox(height: 14),
              _Notice(
                icon: Icons.info_outline,
                accent: scheme.error,
                title: 'Cancelled by the hospital',
                body: appointment.cancellationReason!,
              ),
            ],
            if (appointment.status == AppointmentStatus.checkedIn) ...[
              const SizedBox(height: 14),
              _Notice(
                icon: Icons.support_agent_outlined,
                accent: scheme.onSurfaceVariant,
                title: 'This visit can no longer be cancelled here',
                body: 'Please contact reception on ${HospitalContact.reception}. '
                    'A cancellation at this stage may incur a fee of 50% of the '
                    'visit charge.',
              ),
            ],
            if (appointment.canCancel && onCancel != null) ...[
              const SizedBox(height: 8),
              Align(
                alignment: Alignment.centerRight,
                child: TextButton.icon(
                  onPressed: onCancel,
                  icon: const Icon(Icons.close, size: 17),
                  label: const Text('Cancel visit'),
                  style: TextButton.styleFrom(foregroundColor: scheme.error),
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

class _Notice extends StatelessWidget {
  const _Notice({
    required this.icon,
    required this.accent,
    required this.title,
    required this.body,
  });

  final IconData icon;
  final Color accent;
  final String title;
  final String body;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: accent.withValues(alpha: 0.08),
        borderRadius: BorderRadius.circular(AppTheme.radiusM),
        border: Border.all(color: accent.withValues(alpha: 0.35)),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(icon, size: 18, color: accent),
          const SizedBox(width: 10),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(title, style: theme.textTheme.titleSmall),
                const SizedBox(height: 4),
                Text(body, style: theme.textTheme.bodySmall),
              ],
            ),
          ),
        ],
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
        Skeleton(height: 14, width: 90),
        SizedBox(height: 14),
        Skeleton(height: 128, radius: AppTheme.radiusL),
        SizedBox(height: 10),
        Skeleton(height: 128, radius: AppTheme.radiusL),
      ],
    );
  }
}
