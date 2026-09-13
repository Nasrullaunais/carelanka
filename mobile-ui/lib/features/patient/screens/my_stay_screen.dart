import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import 'package:go_router/go_router.dart';

import '../../../core/auth/auth_controller.dart';
import '../../../core/widgets/async_view.dart';
import '../../../services/api_client/models/my_admission.dart';
import '../patient_routes.dart';
import '../state/my_stay_controller.dart';

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
    final name = context.watch<AuthController>().principal?.displayName;

    return Scaffold(
      appBar: AppBar(
        title: const Text('My stay'),
        actions: [
          IconButton(
            icon: const Icon(Icons.logout),
            tooltip: 'Sign out',
            onPressed: () => context.read<AuthController>().signOut(),
          ),
        ],
      ),
      body: AsyncView<MyStay>(
        state: controller.state,
        onRetry: controller.load,
        builder: (context, stay) => switch (stay) {
          MyStayNotLinked() => const _NotLinkedView(),
          MyStayNoAdmission() => const EmptyView(
              icon: Icons.event_available_outlined,
              message: 'You have no current admission.',
            ),
          MyStayCurrent(:final admission) => _buildAdmission(context, controller, name, admission),
        },
      ),
    );
  }

  Widget _buildAdmission(
    BuildContext context,
    MyStayController controller,
    String? name,
    MyAdmission admission,
  ) {
    return RefreshIndicator(
      onRefresh: controller.load,
      child: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          if (name != null) Text(name, style: Theme.of(context).textTheme.titleLarge),
          const SizedBox(height: 16),
          _StatusCard(admission: admission),
          const SizedBox(height: 16),
          _DetailRow(label: 'Ward', value: admission.wardName),
          _DetailRow(label: 'Bed', value: admission.bedNumber),
          _DetailRow(label: 'Admitted', value: _formatDate(admission.admittedAt)),
          _DetailRow(label: 'Expected arrival', value: _formatDate(admission.expectedArrival)),
          _DetailRow(label: 'Discharged', value: _formatDate(admission.dischargedAt)),
          if (admission.dischargeInstructions != null) ...[
            const SizedBox(height: 16),
            Text('Discharge instructions', style: Theme.of(context).textTheme.titleMedium),
            const SizedBox(height: 8),
            Text(admission.dischargeInstructions!),
          ],
          if (!admission.detailsComplete) ...[
            const SizedBox(height: 16),
            _MissingDetailsCard(missing: admission.missingFields),
          ],
        ],
      ),
    );
  }
}

/// A signup with no medical record behind it is the ordinary state for a new
/// account, so this offers the details form rather than reporting an error.
class _NotLinkedView extends StatelessWidget {
  const _NotLinkedView();

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(Icons.badge_outlined, size: 40, color: theme.colorScheme.outline),
            const SizedBox(height: 12),
            Text(
              'Your login is not linked to a hospital record yet.',
              textAlign: TextAlign.center,
              style: theme.textTheme.bodyMedium,
            ),
            const SizedBox(height: 16),
            FilledButton(
              onPressed: () => context.go(PatientPaths.preRegister),
              child: const Text('Fill in my details'),
            ),
          ],
        ),
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
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('Current status', style: theme.textTheme.labelMedium),
            const SizedBox(height: 4),
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
              style: theme.textTheme.titleSmall?.copyWith(color: theme.colorScheme.onTertiaryContainer),
            ),
            const SizedBox(height: 8),
            ...missing.map((field) => Text(
                  '• $field',
                  style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onTertiaryContainer),
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
            child: Text(label, style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.outline)),
          ),
          Expanded(child: Text(value!, style: theme.textTheme.bodyMedium)),
        ],
      ),
    );
  }
}

String? _formatDate(DateTime? value) {
  if (value == null) return null;
  final local = value.toLocal();
  final date = '${local.day.toString().padLeft(2, '0')}/'
      '${local.month.toString().padLeft(2, '0')}/${local.year}';
  final time = '${local.hour.toString().padLeft(2, '0')}:'
      '${local.minute.toString().padLeft(2, '0')}';
  return '$date at $time';
}
