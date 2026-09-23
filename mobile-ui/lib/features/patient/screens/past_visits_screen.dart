import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/theme/app_theme.dart';
import '../../../core/utils/friendly_date.dart';
import '../../../core/widgets/async_view.dart';
import '../../../core/widgets/phone_width.dart';
import '../../../services/api_client/care_lanka_api.dart';
import '../../../services/api_client/models/my_admission.dart';
import '../services/patient_service.dart';
import '../state/past_visits_controller.dart';
import '../widgets/panels.dart';
import '../widgets/status_presentation.dart';
import 'bill_sheet.dart';

class PastVisitsScreen extends StatelessWidget {
  const PastVisitsScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (context) =>
          PastVisitsController(PatientService(context.read<CareLankaApi>()))
            ..load(),
      child: PhoneWidth(
        child: Scaffold(
          appBar: AppBar(title: const Text('Past visits')),
          body: const PastVisitsList(),
        ),
      ),
    );
  }
}

/// The past-visits list on its own, with no [Scaffold] or [AppBar], so a screen that
/// already has both (like [MyStayScreen] for a patient with no current admission) can
/// embed it directly. Reads [PastVisitsController] from context — the caller provides one.
///
/// [shrinkWrap] renders the cards in a plain, non-scrolling [Column] instead of their own
/// [ListView] — needed when embedding inside another scrollable, which a nested unbounded
/// [ListView] would fail to lay out.
class PastVisitsList extends StatelessWidget {
  const PastVisitsList({super.key, this.padding, this.shrinkWrap = false});

  final EdgeInsetsGeometry? padding;
  final bool shrinkWrap;

  @override
  Widget build(BuildContext context) {
    final controller = context.watch<PastVisitsController>();

    return AsyncView<List<MyAdmission>>(
      state: controller.visits,
      onRetry: controller.load,
      loading: const _Skeleton(),
      builder: (context, visits) {
        if (visits.isEmpty) {
          return const EmptyView(
            icon: Icons.history,
            title: 'No past visits',
            message: 'Completed stays will appear here.',
          );
        }

        final effectivePadding = padding ??
            const EdgeInsets.fromLTRB(AppTheme.gutter, 12, AppTheme.gutter, 32);

        if (shrinkWrap) {
          return Padding(
            padding: effectivePadding,
            child: Column(
              children: [
                for (final visit in visits) ...[
                  _VisitCard(visit: visit),
                  if (visit != visits.last) const SizedBox(height: 10),
                ],
              ],
            ),
          );
        }

        return RefreshIndicator(
          onRefresh: controller.load,
          child: ListView.separated(
            padding: effectivePadding,
            itemCount: visits.length,
            separatorBuilder: (_, __) => const SizedBox(height: 10),
            itemBuilder: (_, index) => _VisitCard(visit: visits[index]),
          ),
        );
      },
    );
  }
}

class _VisitCard extends StatelessWidget {
  const _VisitCard({required this.visit});

  final MyAdmission visit;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;
    final place = [
      visit.wardName,
      visit.bedNumber,
    ].whereType<String>().join(' · ');
    final when = visit.dischargedAt ?? visit.admittedAt;

    return Card(
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: () => Navigator.of(context).push(
          MaterialPageRoute(builder: (_) => PastVisitDetailScreen(visit: visit)),
        ),
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Row(
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
                        style: theme.textTheme.bodySmall?.copyWith(
                          color: scheme.onSurfaceVariant,
                        ),
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
              Icon(Icons.chevron_right, color: scheme.onSurfaceVariant),
            ],
          ),
        ),
      ),
    );
  }
}

class PastVisitDetailScreen extends StatelessWidget {
  const PastVisitDetailScreen({super.key, required this.visit});

  final MyAdmission visit;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;
    final instructions = visit.dischargeInstructions;

    return PhoneWidth(
      child: Scaffold(
        appBar: AppBar(title: const Text('Visit details')),
        body: ListView(
          padding: const EdgeInsets.fromLTRB(AppTheme.gutter, 12, AppTheme.gutter, 32),
          children: [
            StatusChip.admission(
              status: visit.status,
              label: visit.statusText,
              scheme: scheme,
            ),
            const SizedBox(height: 16),
            SectionCard(
              title: 'Location and dates',
              icon: Icons.place_outlined,
              child: Column(
                children: [
                  DetailRow(
                    label: 'Ward',
                    value: visit.wardName,
                    icon: Icons.meeting_room_outlined,
                  ),
                  DetailRow(label: 'Bed', value: visit.bedNumber, icon: Icons.bed_outlined),
                  DetailRow(
                    label: 'Admitted',
                    value: visit.admittedAt == null
                        ? null
                        : FriendlyDate.full(visit.admittedAt!),
                    icon: Icons.login_outlined,
                  ),
                  DetailRow(
                    label: 'Discharged',
                    value: visit.dischargedAt == null
                        ? null
                        : FriendlyDate.full(visit.dischargedAt!),
                    icon: Icons.logout_outlined,
                  ),
                ],
              ),
            ),
            if (instructions != null) ...[
              const SizedBox(height: 16),
              SectionCard(
                title: 'Discharge instructions',
                icon: Icons.assignment_outlined,
                child: Text(instructions, style: theme.textTheme.bodyMedium),
              ),
            ],
            const SizedBox(height: 16),
            OutlinedButton.icon(
              onPressed: () => showBillSheet(
                context,
                service: PatientService(context.read<CareLankaApi>()),
                admissionId: visit.admissionId,
              ),
              icon: const Icon(Icons.receipt_long_outlined, size: 18),
              label: const Text('View bill'),
              style: OutlinedButton.styleFrom(minimumSize: const Size.fromHeight(48)),
            ),
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
    return const Padding(
      padding: EdgeInsets.fromLTRB(AppTheme.gutter, 12, AppTheme.gutter, 32),
      child: Column(
        children: [
          Skeleton(height: 120, radius: AppTheme.radiusL),
          SizedBox(height: 10),
          Skeleton(height: 120, radius: AppTheme.radiusL),
        ],
      ),
    );
  }
}
