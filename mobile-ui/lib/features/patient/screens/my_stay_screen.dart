import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/theme/app_theme.dart';
import '../../../core/utils/friendly_date.dart';
import '../../../core/widgets/async_view.dart';
import '../../../services/api_client/care_lanka_api.dart';
import '../../../services/api_client/models/my_admission.dart';
import '../../../services/api_client/models/my_bill.dart';
import '../services/patient_service.dart';
import '../state/my_stay_controller.dart';
import '../state/past_visits_controller.dart';
import '../state/profile_controller.dart';
import '../widgets/bill_view.dart';
import '../widgets/care_query_card.dart';
import '../widgets/panels.dart';
import 'claim_record_screen.dart';
import 'past_visits_screen.dart';
import '../widgets/stay_journey.dart';
import '../widgets/status_presentation.dart';

class MyStayScreen extends StatelessWidget {
  const MyStayScreen({super.key, this.onBookVisit});

  final VoidCallback? onBookVisit;

  @override
  Widget build(BuildContext context) {
    final controller = context.watch<MyStayController>();
    Future<void> refresh() => controller.load(showLoading: false);

    return Scaffold(
      appBar: AppBar(title: const Text('My stay')),
      body: AsyncView<MyStay>(
        state: controller.state,
        onRetry: controller.load,
        loading: const _StaySkeleton(),
        builder: (context, stay) => switch (stay) {
          MyStayNotLinked() => RefreshableMessage(
            onRefresh: refresh,
            child: EmptyView(
              icon: Icons.badge_outlined,
              title: 'No hospital record',
              message: 'Your account is not yet linked to a hospital record. If the '
                  'hospital registered you at the desk, your stay is waiting on a '
                  'record you can claim with your patient code.',
              action: FilledButton.icon(
                onPressed: () =>
                    openClaimRecord(context, context.read<ProfileController>()),
                icon: const Icon(Icons.badge_outlined),
                label: const Text('I have a patient code'),
                style: FilledButton.styleFrom(minimumSize: const Size(220, 48)),
              ),
            ),
          ),
          MyStayNoAdmission() => _NotAdmitted(onBookVisit: onBookVisit, onRefresh: refresh),
          MyStayCurrent(:final admission, :final bill) => _Admission(
            admission: admission,
            bill: bill,
            onRefresh: refresh,
          ),
        },
      ),
    );
  }
}

// Not admitted right now, so there is nothing current to track — show what there is
// instead: past stays, in place, rather than sending the patient off to Profile for them.
class _NotAdmitted extends StatelessWidget {
  const _NotAdmitted({required this.onBookVisit, required this.onRefresh});

  final VoidCallback? onBookVisit;
  final Future<void> Function() onRefresh;

  @override
  Widget build(BuildContext context) {
    return ChangeNotifierProvider(
      create: (context) =>
          PastVisitsController(PatientService(context.read<CareLankaApi>()))
            ..load(),
      child: RefreshableMessage(
        onRefresh: onRefresh,
        child: ListView(
          padding: const EdgeInsets.fromLTRB(AppTheme.gutter, 4, AppTheme.gutter, 32),
          children: [
            EmptyView(
              icon: Icons.event_available_outlined,
              title: 'Not currently admitted',
              message: 'Your ward, bed and progress will appear here once '
                  'hospital staff admit you.',
              action: onBookVisit == null
                  ? null
                  : FilledButton.icon(
                      onPressed: onBookVisit,
                      icon: const Icon(Icons.add),
                      label: const Text('Book a visit'),
                      style: FilledButton.styleFrom(minimumSize: const Size(200, 48)),
                    ),
            ),
            const SizedBox(height: 24),
            Text('Past visits', style: Theme.of(context).textTheme.titleSmall),
            const SizedBox(height: 4),
            const PastVisitsList(padding: EdgeInsets.only(top: 10), shrinkWrap: true),
          ],
        ),
      ),
    );
  }
}

class _Admission extends StatelessWidget {
  const _Admission({required this.admission, required this.bill, required this.onRefresh});

  final MyAdmission admission;
  final MyBill? bill;
  final Future<void> Function() onRefresh;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;
    final journey = StayJourney.of(admission.status);
    final currentBill = bill;

    final hasPlaceOrTime =
        admission.wardName != null ||
        admission.bedNumber != null ||
        admission.expectedArrival != null ||
        admission.admittedAt != null ||
        admission.dischargedAt != null;

    return RefreshIndicator(
      onRefresh: onRefresh,
      child: ListView(
        padding: const EdgeInsets.fromLTRB(AppTheme.gutter, 4, AppTheme.gutter, 32),
        children: [
          _StatusHeadline(admission: admission),
          const SizedBox(height: 20),
          if (journey.cancelled)
            NoticeBanner(
              icon: Icons.cancel_outlined,
              accent: scheme.error,
              title: 'Admission cancelled',
              body: 'You may book another visit when required.',
            )
          else
            SectionCard(
              title: 'Your progress',
              icon: Icons.route_outlined,
              child: StayJourneyTracker(journey: journey),
            ),
          if (!journey.cancelled) ...[
            const SizedBox(height: 16),
            const CareQueryCard(),
          ],
          if (hasPlaceOrTime) ...[
            const SizedBox(height: 16),
            SectionCard(
              title: 'Location and dates',
              icon: Icons.place_outlined,
              child: Column(
                children: [
                  DetailRow(
                    label: 'Ward',
                    value: admission.wardName,
                    icon: Icons.meeting_room_outlined,
                  ),
                  DetailRow(label: 'Bed', value: admission.bedNumber, icon: Icons.bed_outlined),
                  DetailRow(
                    label: 'Expected',
                    value: admission.expectedArrival == null
                        ? null
                        : FriendlyDate.full(admission.expectedArrival!),
                    icon: Icons.schedule_outlined,
                  ),
                  DetailRow(
                    label: 'Admitted',
                    value: admission.admittedAt == null
                        ? null
                        : FriendlyDate.full(admission.admittedAt!),
                    icon: Icons.login_outlined,
                  ),
                  DetailRow(
                    label: 'Discharged',
                    value: admission.dischargedAt == null
                        ? null
                        : FriendlyDate.full(admission.dischargedAt!),
                    icon: Icons.logout_outlined,
                  ),
                ],
              ),
            ),
          ],
          if (currentBill != null) ...[
            const SizedBox(height: 16),
            SectionCard(
              title: 'Your bill',
              icon: Icons.receipt_long_outlined,
              child: BillView(bill: currentBill),
            ),
          ],
          if (admission.dischargeInstructions != null) ...[
            const SizedBox(height: 16),
            SectionCard(
              title: 'Discharge instructions',
              icon: Icons.assignment_outlined,
              child: Container(
                width: double.infinity,
                padding: const EdgeInsets.all(14),
                decoration: BoxDecoration(
                  color: scheme.surfaceContainerHighest,
                  borderRadius: BorderRadius.circular(AppTheme.radiusM),
                ),
                child: Text(
                  admission.dischargeInstructions!,
                  style: theme.textTheme.bodyMedium,
                ),
              ),
            ),
          ],
        ],
      ),
    );
  }
}

class _StatusHeadline extends StatelessWidget {
  const _StatusHeadline({required this.admission});

  final MyAdmission admission;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;
    final look = StatusLook.ofAdmission(admission.status, scheme);

    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        color: scheme.surface,
        borderRadius: BorderRadius.circular(AppTheme.radiusL),
        border: Border.all(color: scheme.outlineVariant.withValues(alpha: 0.6)),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Container(
            padding: const EdgeInsets.all(10),
            decoration: BoxDecoration(color: look.surface, shape: BoxShape.circle),
            child: Icon(look.icon, size: 22, color: look.color),
          ),
          const SizedBox(width: 14),
          Expanded(
            child: Padding(
              padding: const EdgeInsets.only(top: 2),
              child: Text(admission.statusText, style: theme.textTheme.titleLarge),
            ),
          ),
        ],
      ),
    );
  }
}

class _StaySkeleton extends StatelessWidget {
  const _StaySkeleton();

  @override
  Widget build(BuildContext context) {
    return ListView(
      padding: const EdgeInsets.fromLTRB(AppTheme.gutter, 4, AppTheme.gutter, 32),
      children: const [
        Skeleton(height: 118, radius: AppTheme.radiusL),
        SizedBox(height: 20),
        Skeleton(height: 230, radius: AppTheme.radiusL),
        SizedBox(height: 16),
        Skeleton(height: 150, radius: AppTheme.radiusL),
      ],
    );
  }
}
