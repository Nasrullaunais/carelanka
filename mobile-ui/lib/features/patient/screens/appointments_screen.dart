import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/widgets/async_view.dart';
import '../../../services/api_client/models/my_appointment.dart';
import '../state/appointments_controller.dart';
import 'book_appointment_sheet.dart';
import 'my_details_screen.dart';

class AppointmentsScreen extends StatefulWidget {
  const AppointmentsScreen({super.key});

  @override
  State<AppointmentsScreen> createState() => _AppointmentsScreenState();
}

class _AppointmentsScreenState extends State<AppointmentsScreen> {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      context.read<AppointmentsController>().load();
    });
  }

  Future<void> _book() async {
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

    return Scaffold(
      appBar: AppBar(title: const Text('Appointments')),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: controller.busy ? null : _book,
        icon: const Icon(Icons.add),
        label: const Text('Book a visit'),
      ),
      body: AsyncView<List<MyAppointment>>(
        state: controller.appointments,
        onRetry: controller.load,
        builder: (context, _) {
          final upcoming = controller.upcoming;
          final past = controller.past;

          if (upcoming.isEmpty && past.isEmpty) {
            return const EmptyView(
              icon: Icons.event_available_outlined,
              message: 'You have no appointments yet.',
            );
          }

          return RefreshIndicator(
            onRefresh: controller.load,
            child: ListView(
              padding: const EdgeInsets.only(bottom: 96),
              children: [
                if (upcoming.isNotEmpty) ...[
                  const _SectionHeading('Upcoming'),
                  for (final appointment in upcoming)
                    _AppointmentTile(
                      appointment: appointment,
                      onCancel: controller.busy ? null : () => _cancel(appointment),
                    ),
                ],
                if (past.isNotEmpty) ...[
                  const _SectionHeading('Past'),
                  for (final appointment in past)
                    _AppointmentTile(appointment: appointment, onCancel: null),
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
      padding: const EdgeInsets.fromLTRB(16, 20, 16, 8),
      child: Text(label, style: Theme.of(context).textTheme.titleSmall),
    );
  }
}

class _AppointmentTile extends StatelessWidget {
  const _AppointmentTile({required this.appointment, required this.onCancel});

  final MyAppointment appointment;
  final VoidCallback? onCancel;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Card(
      margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 4),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(formatDateTime(appointment.scheduledAt), style: theme.textTheme.titleMedium),
            const SizedBox(height: 4),
            // status_text is the backend's own wording. Re-writing it here is
            // how the app and the ward end up saying different things.
            Text(
              appointment.statusText,
              style: theme.textTheme.bodySmall
                  ?.copyWith(color: theme.colorScheme.onSurfaceVariant),
            ),
            if (appointment.reason != null) ...[
              const SizedBox(height: 8),
              Text(appointment.reason!, style: theme.textTheme.bodyMedium),
            ],
            // can_cancel is the server's decision, not ours — a scheduled visit
            // is not cancellable once the ward has checked you in.
            if (appointment.canCancel && onCancel != null) ...[
              const SizedBox(height: 8),
              Align(
                alignment: Alignment.centerRight,
                child: TextButton(onPressed: onCancel, child: const Text('Cancel visit')),
              ),
            ],
          ],
        ),
      ),
    );
  }
}
