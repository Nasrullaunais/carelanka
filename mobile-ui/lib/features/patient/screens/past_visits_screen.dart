import 'package:flutter/material.dart';
import 'package:flutter_animate/flutter_animate.dart';
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
      child: const _PastVisitsView(),
    );
  }
}

class _PastVisitsView extends StatelessWidget {
  const _PastVisitsView();

  @override
  Widget build(BuildContext context) {
    final controller = context.watch<PastVisitsController>();
    final theme = Theme.of(context);

    return PhoneWidth(
      child: Scaffold(
        appBar: AppBar(
          backgroundColor: Colors.transparent,
          surfaceTintColor: Colors.transparent,
          elevation: 0,
        ),
        extendBodyBehindAppBar: true,
        body: CustomScrollView(
          slivers: [
            SliverToBoxAdapter(
              child: SafeArea(
                bottom: false,
                child: Padding(
                  padding: const EdgeInsets.fromLTRB(AppTheme.gutter, 16, AppTheme.gutter, 16),
                  child: Text(
                    'Past Visits',
                    style: theme.textTheme.headlineMedium?.copyWith(fontWeight: FontWeight.w800),
                  ).animate().fadeIn().slideY(begin: -0.2),
                ),
              ),
            ),
            SliverFillRemaining(
              hasScrollBody: false,
              child: AsyncView<List<MyAdmission>>(
                state: controller.visits,
                onRetry: controller.load,
                loading: const _Skeleton(),
                builder: (context, visits) {
                  if (visits.isEmpty) {
                    return const EmptyView(
                      icon: Icons.history_rounded,
                      title: 'No past visits',
                      message: 'Completed stays will appear here.',
                    ).animate().fadeIn();
                  }

                  return RefreshIndicator(
                    onRefresh: controller.load,
                    child: ListView.separated(
                      padding: const EdgeInsets.fromLTRB(
                        AppTheme.gutter,
                        4,
                        AppTheme.gutter,
                        32,
                      ),
                      physics: const NeverScrollableScrollPhysics(),
                      shrinkWrap: true,
                      itemCount: visits.length,
                      separatorBuilder: (_, __) => const SizedBox(height: 12),
                      itemBuilder: (_, index) => _VisitCard(visit: visits[index])
                          .animate()
                          .fadeIn(delay: (50 * index).ms)
                          .slideY(begin: 0.1),
                    ),
                  );
                },
              ),
            ),
          ],
        ),
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
    final place = [
      visit.wardName,
      visit.bedNumber,
    ].whereType<String>().join(' · ');
    final when = visit.dischargedAt ?? visit.admittedAt;
    final instructions = visit.dischargeInstructions;

    final look = StatusLook.ofAdmission(visit.status, scheme);
    final accent = scheme.outlineVariant;

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
                        if (when != null) ...[
                          DateBlock(date: when, muted: true),
                          const SizedBox(width: 16),
                        ],
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Row(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                                children: [
                                  if (when != null)
                                    Expanded(
                                      child: Text(
                                        FriendlyDate.dayAndMonth(when),
                                        style: theme.textTheme.titleMedium?.copyWith(
                                          fontWeight: FontWeight.w800,
                                        ),
                                      ),
                                    ),
                                  StatusChip.admission(
                                    status: visit.status,
                                    label: visit.statusText,
                                    scheme: scheme,
                                    compact: true,
                                  ),
                                ],
                              ),
                              if (place.isNotEmpty) ...[
                                const SizedBox(height: 4),
                                Text(
                                  place,
                                  style: theme.textTheme.bodySmall?.copyWith(
                                    color: scheme.onSurfaceVariant,
                                    fontWeight: FontWeight.w500,
                                  ),
                                ),
                              ],
                            ],
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: 16),
                    Row(
                      children: [
                        if (instructions != null)
                          Expanded(
                            child: OutlinedButton.icon(
                              onPressed: () => showDialog<void>(
                                context: context,
                                builder: (dialogContext) => AlertDialog(
                                  title: Text(
                                    _instructionsLabel,
                                    style: theme.textTheme.titleMedium?.copyWith(fontWeight: FontWeight.w700),
                                  ),
                                  content: SingleChildScrollView(
                                    child: Text(instructions),
                                  ),
                                  actions: [
                                    TextButton(
                                      onPressed: () =>
                                          Navigator.of(dialogContext).pop(),
                                      child: const Text('Close'),
                                    ),
                                  ],
                                ),
                              ),
                              icon: const Icon(Icons.assignment_outlined, size: 18),
                              label: const Text('Instructions'),
                              style: OutlinedButton.styleFrom(
                                minimumSize: const Size.fromHeight(44),
                                shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(999)),
                              ),
                            ),
                          ),
                        if (instructions != null) const SizedBox(width: 10),
                        Expanded(
                          child: OutlinedButton.icon(
                            onPressed: () => showBillSheet(
                              context,
                              service: PatientService(context.read<CareLankaApi>()),
                              admissionId: visit.admissionId,
                            ),
                            icon: const Icon(Icons.receipt_long_outlined, size: 18),
                            label: const Text('Bill'),
                            style: OutlinedButton.styleFrom(
                              minimumSize: const Size.fromHeight(44),
                              shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(999)),
                            ),
                          ),
                        ),
                      ],
                    ),
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

class _Skeleton extends StatelessWidget {
  const _Skeleton();

  @override
  Widget build(BuildContext context) {
    return ListView(
      padding: const EdgeInsets.fromLTRB(
        AppTheme.gutter,
        12,
        AppTheme.gutter,
        32,
      ),
      children: const [
        Skeleton(height: 140, radius: AppTheme.radiusL),
        SizedBox(height: 12),
        Skeleton(height: 140, radius: AppTheme.radiusL),
      ],
    );
  }
}
