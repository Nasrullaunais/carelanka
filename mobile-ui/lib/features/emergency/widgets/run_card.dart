import 'package:flutter/material.dart';

import '../../../core/theme/app_theme.dart';
import '../../../services/api_client/models/dispatch_detail.dart';
import '../models/run_step.dart';
import 'label_value_row.dart';
import 'priority_pill.dart';
import 'run_section.dart';

class RunCard extends StatelessWidget {
  const RunCard({
    super.key,
    required this.run,
    required this.busy,
    required this.onStep,
    required this.onDecline,
    required this.onNavigate,
    this.onEndAtScene,
    this.onCallCaller,
  });

  final DispatchDetail run;
  final bool busy;
  final VoidCallback onStep;
  final VoidCallback onDecline;
  final VoidCallback onNavigate;
  final VoidCallback? onEndAtScene;
  final ValueChanged<String>? onCallCaller;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final status = run.status;
    final step = status?.nextStep;
    final phone = run.callerPhone?.trim();

    final details = ListView(
      padding: EdgeInsets.fromLTRB(
        AppTheme.gutter,
        8,
        AppTheme.gutter,
        AppTheme.gutter +
            (step == null ? MediaQuery.paddingOf(context).bottom : 0),
      ),
      children: [
        Text(status?.crewLabel ?? '', style: theme.textTheme.headlineSmall),
        const SizedBox(height: 10),
        Align(
          alignment: Alignment.centerLeft,
          child: PriorityPill(priority: run.callPriority),
        ),
        const SizedBox(height: 20),
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
            if (phone != null && phone.isNotEmpty && onCallCaller != null)
              Padding(
                padding: const EdgeInsets.only(top: 10, bottom: 4),
                child: OutlinedButton.icon(
                  onPressed: () => onCallCaller!(phone),
                  icon: const Icon(Icons.phone_outlined),
                  label: Text('Call the caller · $phone'),
                ),
              ),
          ],
        ),
        const SizedBox(height: 12),
        RunSection(
          title: 'The run',
          children: [
            LabelValueRow(label: 'Ambulance', value: run.ambulanceRegistration),
            if (run.sceneAddressLabel?.trim().isNotEmpty == true)
              LabelValueRow(
                label: 'Scene',
                value: run.sceneAddressLabel,
                stacked: true,
              ),
            if (run.destinationLabel?.trim().isNotEmpty == true &&
                run.destinationLabel != run.sceneAddressLabel)
              LabelValueRow(
                label: 'Going to',
                value: run.destinationLabel,
                stacked: true,
              ),
            LabelValueRow(
              label: 'Crew on board',
              value: run.crewCount?.toString(),
            ),
          ],
        ),
        const SizedBox(height: 8),
        if ((status?.canEndAtScene ?? false) && onEndAtScene != null) ...[
          const SizedBox(height: 12),
          OutlinedButton(
            onPressed: busy ? null : onEndAtScene,
            style: OutlinedButton.styleFrom(
              minimumSize: const Size.fromHeight(52),
            ),
            child: const Text('End without going to hospital'),
          ),
        ],
        if (status?.canNavigate ?? false) ...[
          const SizedBox(height: 12),
          OutlinedButton.icon(
            onPressed: busy ? null : onNavigate,
            icon: const Icon(Icons.navigation_outlined),
            label: const Text('Open in Google Maps'),
            style: OutlinedButton.styleFrom(
              minimumSize: const Size.fromHeight(52),
            ),
          ),
        ],
        if (status?.canDecline ?? false) ...[
          const SizedBox(height: 12),
          TextButton(
            onPressed: busy ? null : onDecline,
            style: TextButton.styleFrom(
              minimumSize: const Size.fromHeight(48),
              foregroundColor: theme.colorScheme.error,
            ),
            child: const Text('I cannot take this run'),
          ),
        ],
      ],
    );

    if (step == null) return details;
    return Column(
      children: [
        Expanded(child: details),
        DecoratedBox(
          decoration: BoxDecoration(
            border: Border(
              top: BorderSide(color: theme.colorScheme.outlineVariant),
            ),
          ),
          child: SafeArea(
            top: false,
            child: Padding(
              padding: const EdgeInsets.fromLTRB(
                AppTheme.gutter,
                12,
                AppTheme.gutter,
                12,
              ),
              child: FilledButton(
                onPressed: busy ? null : onStep,
                style: FilledButton.styleFrom(
                  minimumSize: const Size.fromHeight(56),
                ),
                child: busy
                    ? const SizedBox.square(
                        dimension: 22,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    : Text(step.label),
              ),
            ),
          ),
        ),
      ],
    );
  }
}
