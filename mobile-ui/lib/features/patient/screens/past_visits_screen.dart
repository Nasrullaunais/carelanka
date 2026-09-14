import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/theme/app_theme.dart';
import '../../../core/utils/friendly_date.dart';
import '../../../core/widgets/async_view.dart';
import '../../../services/api_client/care_lanka_api.dart';
import '../../../services/api_client/models/my_admission.dart';
import '../services/patient_service.dart';
import '../state/past_visits_controller.dart';
import '../widgets/panels.dart';
import '../widgets/status_presentation.dart';

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
        loading: const _Skeleton(),
        builder: (context, visits) {
          if (visits.isEmpty) {
            return const EmptyView(
              icon: Icons.history,
              title: 'No finished visits',
              message: 'Once a stay ends it moves here, with whatever the ward '
                  'sent you home with.',
            );
          }

          return RefreshIndicator(
            onRefresh: controller.load,
            child: ListView.separated(
              padding: const EdgeInsets.fromLTRB(AppTheme.gutter, 12, AppTheme.gutter, 32),
              itemCount: visits.length,
              separatorBuilder: (_, __) => const SizedBox(height: 10),
              itemBuilder: (_, index) => _VisitCard(visit: visits[index]),
            ),
          );
        },
      ),
    );
  }
}

class _VisitCard extends StatelessWidget {
  const _VisitCard({required this.visit});

  final MyAdmission visit;

  static const _instructionsLabel = 'Discharge instructions';

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;
    final place = [visit.wardName, visit.bedNumber].whereType<String>().join(' · ');
    final when = visit.dischargedAt ?? visit.admittedAt;
    final instructions = visit.dischargeInstructions;

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                if (when != null) ...[
                  DateBlock(date: when, muted: true),
                  const SizedBox(width: 14),
                ],
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      if (when != null)
                        Text(
                          FriendlyDate.dayAndMonth(when),
                          style: theme.textTheme.titleMedium,
                        ),
                      if (place.isNotEmpty) ...[
                        const SizedBox(height: 2),
                        Text(
                          place,
                          style: theme.textTheme.bodySmall
                              ?.copyWith(color: scheme.onSurfaceVariant),
                        ),
                      ],
                      const SizedBox(height: 10),
                      StatusChip.admission(
                        status: visit.status,
                        label: visit.statusText,
                        scheme: scheme,
                        compact: true,
                      ),
                    ],
                  ),
                ),
              ],
            ),
            if (instructions != null) ...[
              const SizedBox(height: 12),
              OutlinedButton.icon(
                onPressed: () => showDialog<void>(
                  context: context,
                  builder: (dialogContext) => AlertDialog(
                    title: const Text(_instructionsLabel),
                    content: SingleChildScrollView(child: Text(instructions)),
                    actions: [
                      TextButton(
                        onPressed: () => Navigator.of(dialogContext).pop(),
                        child: const Text('Close'),
                      ),
                    ],
                  ),
                ),
                icon: const Icon(Icons.assignment_outlined, size: 18),
                label: const Text(_instructionsLabel),
                style: OutlinedButton.styleFrom(minimumSize: const Size.fromHeight(44)),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

class _Skeleton extends StatelessWidget {
  const _Skeleton();

  @override
  Widget build(BuildContext context) {
    return ListView(
      padding: const EdgeInsets.fromLTRB(AppTheme.gutter, 12, AppTheme.gutter, 32),
      children: const [
        Skeleton(height: 120, radius: AppTheme.radiusL),
        SizedBox(height: 10),
        Skeleton(height: 120, radius: AppTheme.radiusL),
      ],
    );
  }
}
