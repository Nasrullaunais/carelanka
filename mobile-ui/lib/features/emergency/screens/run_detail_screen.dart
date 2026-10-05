import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/theme/app_theme.dart';
import '../../../core/utils/friendly_date.dart';
import '../../../core/widgets/async_view.dart';
import '../../../services/api_client/models/dispatch_detail.dart';
import '../../../services/api_client/models/dispatch_status.dart';
import '../models/run_step.dart';
import '../state/run_detail_controller.dart';
import '../widgets/label_value_row.dart';
import '../widgets/priority_pill.dart';
import '../widgets/run_section.dart';

class RunDetailScreen extends StatefulWidget {
  const RunDetailScreen({super.key});

  @override
  State<RunDetailScreen> createState() => _RunDetailScreenState();
}

class _RunDetailScreenState extends State<RunDetailScreen> {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) context.read<RunDetailController>().load();
    });
  }

  @override
  Widget build(BuildContext context) {
    final controller = context.watch<RunDetailController>();

    return Scaffold(
      appBar: AppBar(title: const Text('Run details')),
      body: AsyncView(
        state: controller.state,
        onRetry: controller.load,
        builder: (context, run) => _RunDetail(run: run),
      ),
    );
  }
}

class _RunDetail extends StatelessWidget {
  const _RunDetail({required this.run});

  final DispatchDetail run;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final dispatchedAt = run.dispatchedAt;
    final reason = switch (run.status) {
      DispatchStatus.declined => run.declinedReason,
      DispatchStatus.cancelled => run.cancellationReason,
      DispatchStatus.reassigned => run.reassignmentReason,
      _ => null,
    };
    // A run that was called off or given away did not decide how the call ended.
    final endedTheCall =
        run.status == DispatchStatus.handedOver ||
        run.status == DispatchStatus.closedAtScene;

    return ListView(
      padding: EdgeInsets.fromLTRB(
        AppTheme.gutter,
        8,
        AppTheme.gutter,
        AppTheme.gutter + MediaQuery.paddingOf(context).bottom,
      ),
      children: [
        Text(run.status?.crewLabel ?? '', style: theme.textTheme.headlineSmall),
        const SizedBox(height: 10),
        Wrap(
          spacing: 10,
          runSpacing: 6,
          crossAxisAlignment: WrapCrossAlignment.center,
          children: [
            PriorityPill(priority: run.callPriority),
            if (dispatchedAt != null)
              Text(
                FriendlyDate.full(dispatchedAt),
                style: theme.textTheme.bodyMedium?.copyWith(
                  color: theme.colorScheme.onSurfaceVariant,
                ),
              ),
          ],
        ),
        const SizedBox(height: 20),
        for (final section in [
          RunSection(
            title: 'The emergency',
            children: [
              Text(
                run.callDetails?.trim().isNotEmpty == true
                    ? run.callDetails!
                    : 'No details were given by the caller.',
                style: theme.textTheme.bodyLarge,
              ),
              if (run.patientName?.trim().isNotEmpty == true ||
                  run.callerName?.trim().isNotEmpty == true) ...[
                const SizedBox(height: 10),
                const Divider(),
                const SizedBox(height: 6),
              ],
              if (run.patientName?.trim().isNotEmpty == true)
                LabelValueRow(label: 'Patient', value: run.patientName),
              if (run.callerName?.trim().isNotEmpty == true)
                LabelValueRow(label: 'Caller', value: run.callerName),
            ],
          ),
          RunSection(
            title: 'The run',
            children: [
              LabelValueRow(
                label: 'Ambulance',
                value: run.ambulanceRegistration,
              ),
              if (run.sceneAddressLabel?.trim().isNotEmpty == true)
                LabelValueRow(
                  label: 'Scene',
                  value: run.sceneAddressLabel,
                  stacked: true,
                ),
              LabelValueRow(
                label: 'Crew on board',
                value: run.crewCount?.toString(),
              ),
            ],
          ),
          RunSection(
            title: 'Times',
            children: [
              if (dispatchedAt != null)
                LabelValueRow(
                  label: 'Sent',
                  value: FriendlyDate.time(dispatchedAt),
                ),
              if (run.acknowledgedAt case final at?)
                LabelValueRow(label: 'Accepted', value: FriendlyDate.time(at)),
              if (run.completedAt case final at?)
                LabelValueRow(label: 'Finished', value: FriendlyDate.time(at)),
            ],
          ),
          RunSection(
            title: 'How it ended',
            children: [
              if (endedTheCall)
                if (run.callOutcome?.label case final outcome?)
                  LabelValueRow(label: 'Outcome', value: outcome),
              if (run.patientCondition?.trim().isNotEmpty == true)
                LabelValueRow(
                  label: 'Patient condition',
                  value: run.patientCondition,
                ),
              if (run.handoverNotes?.trim().isNotEmpty == true) ...[
                const SizedBox(height: 8),
                Text('Handover notes', style: theme.textTheme.labelLarge),
                const SizedBox(height: 4),
                Text(run.handoverNotes!),
              ],
              if (endedTheCall &&
                  run.callOutcomeNotes?.trim().isNotEmpty == true) ...[
                const SizedBox(height: 8),
                Text('Notes', style: theme.textTheme.labelLarge),
                const SizedBox(height: 4),
                Text(run.callOutcomeNotes!),
              ],
              if (reason?.trim().isNotEmpty == true) ...[
                const SizedBox(height: 8),
                Text('Reason given', style: theme.textTheme.labelLarge),
                const SizedBox(height: 4),
                Text(reason!),
              ],
            ],
          ),
        ])
          if (section.children.isNotEmpty) ...[
            section,
            const SizedBox(height: 12),
          ],
      ],
    );
  }
}
