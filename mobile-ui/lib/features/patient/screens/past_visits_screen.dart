import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/widgets/async_view.dart';
import '../../../services/api_client/care_lanka_api.dart';
import '../../../services/api_client/models/my_admission.dart';
import '../services/patient_service.dart';
import '../state/past_visits_controller.dart';
import 'my_details_screen.dart';

class PastVisitsScreen extends StatelessWidget {
  const PastVisitsScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (context) =>
          PastVisitsController(PatientService(context.read<CareLankaApi>()))..load(),
      child: const _PastVisitsView(),
    );
  }
}

class _PastVisitsView extends StatelessWidget {
  const _PastVisitsView();

  @override
  Widget build(BuildContext context) {
    final controller = context.watch<PastVisitsController>();

    return Scaffold(
      appBar: AppBar(title: const Text('Past visits')),
      body: AsyncView<List<MyAdmission>>(
        state: controller.visits,
        onRetry: controller.load,
        builder: (context, visits) {
          if (visits.isEmpty) {
            return const EmptyView(
              icon: Icons.history,
              message: 'You have no finished visits yet.',
            );
          }

          return RefreshIndicator(
            onRefresh: controller.load,
            child: ListView.separated(
              itemCount: visits.length,
              separatorBuilder: (_, __) => const Divider(height: 1),
              itemBuilder: (_, index) => _VisitTile(visit: visits[index]),
            ),
          );
        },
      ),
    );
  }
}

class _VisitTile extends StatelessWidget {
  const _VisitTile({required this.visit});

  final MyAdmission visit;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final place = [visit.wardName, visit.bedNumber].whereType<String>().join(' · ');
    final when = visit.dischargedAt ?? visit.admittedAt;

    return ListTile(
      title: Text(visit.statusText),
      subtitle: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          if (when != null) Text(formatDateTime(when), style: theme.textTheme.bodySmall),
          if (place.isNotEmpty) Text(place, style: theme.textTheme.bodySmall),
        ],
      ),
      trailing: visit.dischargeInstructions == null ? null : const Icon(Icons.chevron_right),
      onTap: visit.dischargeInstructions == null
          ? null
          : () => showDialog<void>(
                context: context,
                builder: (_) => AlertDialog(
                  title: const Text('Discharge instructions'),
                  content: SingleChildScrollView(child: Text(visit.dischargeInstructions!)),
                  actions: [
                    TextButton(
                      onPressed: () => Navigator.of(context).pop(),
                      child: const Text('Close'),
                    ),
                  ],
                ),
              ),
    );
  }
}
