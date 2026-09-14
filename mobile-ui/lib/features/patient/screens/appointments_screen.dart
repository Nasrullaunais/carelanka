import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/theme/app_theme.dart';
import '../../../core/utils/friendly_date.dart';
import '../../../core/widgets/async_view.dart';
import '../../../services/api_client/models/my_appointment.dart';
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
  /// Home's "Book a visit" opens the same sheet this screen owns, so the flow
  /// lives in one place rather than being written twice.
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
      content: Text(error?.message ?? 'Your visit is booked.'),
    ));
  }

  Future<void> _cancel(MyAppointment appointment) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Text('Cancel this visit?'),
        content: Text(
          'Your booking for ${FriendlyDate.full(appointment.scheduledAt)} will '
          'be called off. You can book another afterwards.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(false),
            child: const Text('Keep it'),
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
      content: Text(error?.message ?? 'Your visit is cancelled.'),
    ));
  }

  @override
  Widget build(BuildContext context) {
    final controller = context.watch<AppointmentsController>();
    // The API answers an empty page rather than an error for an unlinked
    // account, so an empty list alone cannot tell these two apart - and
    // offering "Book a visit" here would earn a 409 on the first tap.
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
              title: 'Finish setting up first',
              message: 'The hospital needs your details before it can take a '
                  'booking from you.',
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
              title: 'No visits yet',
              message: 'Book one and it will show up here with everything the '
                  'hospital needs from you.',
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
                  const _SectionHeading('Coming up'),
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
                  const _SectionHeading('Earlier'),
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
                      // status_text is the server's wording for the state
                      // machine. The chip colours it; it never rewrites it.
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
            // can_cancel is the server's decision, not ours — a scheduled visit
            // stops being cancellable once the ward has checked you in.
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
