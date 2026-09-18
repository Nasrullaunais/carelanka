import 'package:flutter/material.dart';
import 'package:flutter_animate/flutter_animate.dart';
import 'package:provider/provider.dart';

import '../../../core/theme/app_theme.dart';
import '../../../core/utils/friendly_date.dart';
import '../../../core/widgets/async_view.dart';
import '../../../services/api_client/models/my_admission.dart';
import '../../../services/api_client/models/my_bill.dart';
import '../state/my_stay_controller.dart';
import '../state/profile_controller.dart';
import '../widgets/bill_view.dart';
import '../widgets/panels.dart';
import 'claim_record_screen.dart';
import '../widgets/stay_journey.dart';
import '../widgets/status_presentation.dart';

class MyStayScreen extends StatelessWidget {
  const MyStayScreen({super.key, this.onBookVisit});

  final VoidCallback? onBookVisit;

  @override
  Widget build(BuildContext context) {
    final controller = context.watch<MyStayController>();
    Future<void> refresh() => controller.load(showLoading: false);
    final theme = Theme.of(context);

    return Scaffold(
      body: CustomScrollView(
        slivers: [
          SliverToBoxAdapter(
            child: SafeArea(
              bottom: false,
              child: Padding(
                padding: const EdgeInsets.fromLTRB(AppTheme.gutter, 24, AppTheme.gutter, 16),
                child: Text(
                  'My Stay',
                  style: theme.textTheme.headlineMedium?.copyWith(fontWeight: FontWeight.w800),
                ).animate().fadeIn().slideY(begin: -0.2),
              ),
            ),
          ),
          SliverFillRemaining(
            hasScrollBody: false,
            child: AsyncView<MyStay>(
              state: controller.state,
              onRetry: controller.load,
              loading: const _StaySkeleton(),
              builder: (context, stay) => switch (stay) {
                MyStayNotLinked() => RefreshableMessage(
                  onRefresh: refresh,
                  child: EmptyView(
                    icon: Icons.badge_rounded,
                    title: 'No hospital record',
                    message: 'Your account is not yet linked to a hospital record. If the '
                        'hospital registered you at the desk, your stay is waiting on a '
                        'record you can claim with your patient code.',
                    action: FilledButton.icon(
                      onPressed: () =>
                          openClaimRecord(context, context.read<ProfileController>()),
                      icon: const Icon(Icons.badge_outlined),
                      label: const Text('I have a patient code'),
                      style: FilledButton.styleFrom(minimumSize: const Size(240, 52)),
                    ),
                  ),
                ),
                MyStayNoAdmission() => RefreshableMessage(
                  onRefresh: refresh,
                  child: EmptyView(
                    icon: Icons.event_available_rounded,
                    title: 'Not currently admitted',
                    message: 'Your ward, bed and progress will appear here once '
                        'hospital staff admit you.',
                    action: onBookVisit == null
                        ? null
                        : FilledButton.icon(
                            onPressed: onBookVisit,
                            icon: const Icon(Icons.add),
                            label: const Text('Book a visit'),
                            style: FilledButton.styleFrom(minimumSize: const Size(200, 52)),
                          ),
                  ),
                ),
                MyStayCurrent(:final admission, :final bill) => _Admission(
                  admission: admission,
                  bill: bill,
                  onRefresh: refresh,
                ),
              },
            ),
          ),
        ],
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
        padding: const EdgeInsets.fromLTRB(AppTheme.gutter, 4, AppTheme.gutter, 104),
        physics: const NeverScrollableScrollPhysics(),
        shrinkWrap: true,
        children: [
          _StatusHeadline(admission: admission).animate().fadeIn(duration: 400.ms).slideY(begin: 0.1),
          const SizedBox(height: 24),
          if (journey.cancelled)
            NoticeBanner(
              icon: Icons.cancel_outlined,
              accent: scheme.error,
              title: 'Admission cancelled',
              body: 'You may book another visit when required.',
            ).animate().fadeIn(delay: 100.ms)
          else
            SectionCard(
              title: 'Your progress',
              icon: Icons.route_outlined,
              padding: const EdgeInsets.all(24),
              child: StayJourneyTracker(journey: journey),
            ).animate().fadeIn(delay: 100.ms).slideY(begin: 0.1),
          if (hasPlaceOrTime) ...[
            const SizedBox(height: 20),
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
            ).animate().fadeIn(delay: 200.ms).slideY(begin: 0.1),
          ],
          if (currentBill != null) ...[
            const SizedBox(height: 20),
            SectionCard(
              title: 'Your bill',
              icon: Icons.receipt_long_outlined,
              child: BillView(bill: currentBill),
            ).animate().fadeIn(delay: 300.ms).slideY(begin: 0.1),
          ],
          if (admission.dischargeInstructions != null) ...[
            const SizedBox(height: 20),
            SectionCard(
              title: 'Discharge instructions',
              icon: Icons.assignment_outlined,
              child: Container(
                width: double.infinity,
                padding: const EdgeInsets.all(16),
                decoration: BoxDecoration(
                  color: scheme.surfaceContainerHighest.withValues(alpha: 0.3),
                  borderRadius: BorderRadius.circular(AppTheme.radiusM),
                  border: Border.all(color: scheme.outlineVariant.withValues(alpha: 0.2)),
                ),
                child: Text(
                  admission.dischargeInstructions!,
                  style: theme.textTheme.bodyMedium?.copyWith(height: 1.5),
                ),
              ),
            ).animate().fadeIn(delay: 400.ms).slideY(begin: 0.1),
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
      padding: const EdgeInsets.all(24),
      decoration: BoxDecoration(
        gradient: LinearGradient(
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
          colors: [
            look.color,
            look.color.withValues(alpha: 0.8),
          ],
        ),
        borderRadius: BorderRadius.circular(AppTheme.radiusL),
        boxShadow: [
          BoxShadow(
            color: look.color.withValues(alpha: 0.3),
            blurRadius: 20,
            offset: const Offset(0, 8),
          ),
        ],
      ),
      child: Row(
        children: [
          Container(
            padding: const EdgeInsets.all(12),
            decoration: BoxDecoration(
              color: Colors.white.withValues(alpha: 0.2),
              shape: BoxShape.circle,
            ),
            child: Icon(look.icon, size: 28, color: Colors.white),
          ),
          const SizedBox(width: 16),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'Current Status',
                  style: theme.textTheme.labelMedium?.copyWith(
                    color: Colors.white.withValues(alpha: 0.8),
                    fontWeight: FontWeight.w600,
                    letterSpacing: 0.5,
                  ),
                ),
                const SizedBox(height: 2),
                Text(
                  admission.statusText,
                  style: theme.textTheme.titleLarge?.copyWith(
                    color: Colors.white,
                    fontWeight: FontWeight.w800,
                  ),
                ),
              ],
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
        Skeleton(height: 104, radius: AppTheme.radiusL),
        SizedBox(height: 24),
        Skeleton(height: 300, radius: AppTheme.radiusL),
        SizedBox(height: 20),
        Skeleton(height: 200, radius: AppTheme.radiusL),
      ],
    );
  }
}
