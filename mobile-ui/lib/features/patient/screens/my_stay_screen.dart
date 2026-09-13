import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/widgets/async_view.dart';
import '../../../services/api_client/models/my_admission.dart';
import '../state/my_stay_controller.dart';
import 'my_details_screen.dart';

class MyStayScreen extends StatefulWidget {
  const MyStayScreen({super.key});

  @override
  State<MyStayScreen> createState() => _MyStayScreenState();
}

class _MyStayScreenState extends State<MyStayScreen> {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      context.read<MyStayController>().load();
    });
  }

  @override
  Widget build(BuildContext context) {
    final controller = context.watch<MyStayController>();

    return Scaffold(
      appBar: AppBar(title: const Text('My stay')),
      body: AsyncView<MyStay>(
        state: controller.state,
        onRetry: controller.load,
        builder: (context, stay) => switch (stay) {
          MyStayNotLinked() => const EmptyView(
              icon: Icons.badge_outlined,
              message: 'Your login is not linked to a hospital record yet.',
            ),
          MyStayNoAdmission() => const EmptyView(
              icon: Icons.event_available_outlined,
              message: 'You are not admitted right now.\nBook a visit to get started.',
            ),
          MyStayCurrent(:final admission) => _Admission(
              admission: admission,
              onRefresh: controller.load,
            ),
        },
      ),
    );
  }
}

class _Admission extends StatelessWidget {
  const _Admission({required this.admission, required this.onRefresh});

  final MyAdmission admission;
  final Future<void> Function() onRefresh;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return RefreshIndicator(
      onRefresh: onRefresh,
      child: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          _StatusCard(admission: admission),
          const SizedBox(height: 16),
          _DetailRow(label: 'Ward', value: admission.wardName),
          _DetailRow(label: 'Bed', value: admission.bedNumber),
          _DetailRow(
            label: 'Admitted',
            value: admission.admittedAt == null ? null : formatDateTime(admission.admittedAt!),
          ),
          _DetailRow(
            label: 'Expected arrival',
            value: admission.expectedArrival == null
                ? null
                : formatDateTime(admission.expectedArrival!),
          ),
          _DetailRow(
            label: 'Discharged',
            value:
                admission.dischargedAt == null ? null : formatDateTime(admission.dischargedAt!),
          ),
          if (admission.dischargeInstructions != null) ...[
            const SizedBox(height: 24),
            Text('Discharge instructions', style: theme.textTheme.titleMedium),
            const SizedBox(height: 8),
            Text(admission.dischargeInstructions!),
          ],
          if (!admission.detailsComplete) ...[
            const SizedBox(height: 24),
            _MissingDetailsCard(missing: admission.missingFields),
          ],
        ],
      ),
    );
  }
}

class _StatusCard extends StatelessWidget {
  const _StatusCard({required this.admission});

  final MyAdmission admission;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'Current status',
              style: theme.textTheme.labelMedium
                  ?.copyWith(color: theme.colorScheme.onSurfaceVariant),
            ),
            const SizedBox(height: 6),
            // status_text is the backend's own wording for the state machine —
            // never re-word it here, or the app and the ward disagree.
            Text(admission.statusText, style: theme.textTheme.headlineSmall),
          ],
        ),
      ),
    );
  }
}

class _MissingDetailsCard extends StatelessWidget {
  const _MissingDetailsCard({required this.missing});

  final List<String> missing;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Card(
      color: theme.colorScheme.tertiaryContainer,
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'The ward still needs some details',
              style: theme.textTheme.titleSmall
                  ?.copyWith(color: theme.colorScheme.onTertiaryContainer),
            ),
            const SizedBox(height: 8),
            ...missing.map((field) => Text(
                  '• $field',
                  style: theme.textTheme.bodySmall
                      ?.copyWith(color: theme.colorScheme.onTertiaryContainer),
                )),
          ],
        ),
      ),
    );
  }
}

class _DetailRow extends StatelessWidget {
  const _DetailRow({required this.label, required this.value});

  final String label;
  final String? value;

  @override
  Widget build(BuildContext context) {
    if (value == null) return const SizedBox.shrink();

    final theme = Theme.of(context);

    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 6),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SizedBox(
            width: 140,
            child: Text(
              label,
              style: theme.textTheme.bodyMedium
                  ?.copyWith(color: theme.colorScheme.onSurfaceVariant),
            ),
          ),
          Expanded(child: Text(value!, style: theme.textTheme.bodyMedium)),
        ],
      ),
    );
  }
}
