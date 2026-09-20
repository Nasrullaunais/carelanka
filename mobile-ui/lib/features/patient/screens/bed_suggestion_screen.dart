import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/auth/auth_controller.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/widgets/async_data.dart';
import '../../../core/widgets/async_view.dart';
import '../../../core/widgets/phone_width.dart';
import '../../../services/api_client/models/admission_category.dart';
import '../../../services/api_client/models/admission_urgency.dart';
import '../../../services/api_client/models/bed_suggestion_patient.dart';
import '../../../services/api_client/models/bed_workflow_status.dart';
import '../../../services/api_client/models/bed_workflow_summary.dart';
import '../../../services/api_client/models/gender.dart';
import '../../../services/api_client/models/principal_role.dart';
import '../../../services/api_client/models/suggested_bed.dart';
import '../state/bed_suggestion_controller.dart';

/// Who the patient is, the agent's suggested bed and every alternative — each with its own
/// "Use this bed" button. Started either from a row already on the worklist ([admissionId]) or
/// by typing an NIC or patient code off the hospital slip.
class BedSuggestionScreen extends StatefulWidget {
  const BedSuggestionScreen({
    super.key,
    this.admissionId,
    this.patientName,
  });

  final String? admissionId;
  final String? patientName;

  @override
  State<BedSuggestionScreen> createState() => _BedSuggestionScreenState();
}

class _BedSuggestionScreenState extends State<BedSuggestionScreen> {
  final _identifier = TextEditingController();

  @override
  void initState() {
    super.initState();
    final admissionId = widget.admissionId;
    if (admissionId != null) {
      WidgetsBinding.instance.addPostFrameCallback((_) {
        context.read<BedSuggestionController>().start(admissionId: admissionId);
      });
    }
  }

  @override
  void dispose() {
    _identifier.dispose();
    super.dispose();
  }

  void _startFromIdentifier() {
    final value = _identifier.text.trim();
    if (value.isEmpty) return;
    context.read<BedSuggestionController>().start(patientIdentifier: value);
  }

  Future<void> _confirm(BedWorkflowSummary summary, SuggestedBed bed) async {
    final controller = context.read<BedSuggestionController>();
    final admissionId = widget.admissionId ?? summary.patient?.admissionId;
    final workflowId = summary.workflowId;
    if (admissionId == null || workflowId == null) return;

    final ok = await controller.assign(
      admissionId: admissionId,
      bedId: bed.bedId,
      workflowId: workflowId,
    );

    if (!mounted) return;

    if (!ok) {
      final message = controller.assignError?.message ?? 'Could not assign the bed.';
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(message)));
      return;
    }

    final assignment = controller.assigned!;
    Navigator.of(context).pop(assignment);
  }

  @override
  Widget build(BuildContext context) {
    final controller = context.watch<BedSuggestionController>();
    final role = context.watch<AuthController>().principal?.role;

    return PhoneWidth(
      child: Scaffold(
        appBar: AppBar(title: const Text('Suggest a bed')),
        body: Padding(
          padding: const EdgeInsets.all(AppTheme.gutter),
          child: widget.admissionId == null && controller.workflow == null
              ? _IdentifierForm(
                  controller: _identifier,
                  starting: controller.starting,
                  error: controller.startError?.message,
                  onSubmit: _startFromIdentifier,
                )
              : _WorkflowView(
                  workflow: controller.workflow,
                  onRetry: () => widget.admissionId != null
                      ? context
                          .read<BedSuggestionController>()
                          .start(admissionId: widget.admissionId)
                      : _startFromIdentifier(),
                  assigning: controller.assigning,
                  role: role,
                  onConfirm: _confirm,
                ),
        ),
      ),
    );
  }
}

class _IdentifierForm extends StatelessWidget {
  const _IdentifierForm({
    required this.controller,
    required this.starting,
    required this.error,
    required this.onSubmit,
  });

  final TextEditingController controller;
  final bool starting;
  final String? error;
  final VoidCallback onSubmit;

  @override
  Widget build(BuildContext context) {
    return ListView(
      children: [
        Text(
          'Type the NIC or patient code off the hospital slip. The agent looks up who they are '
          'and suggests a bed the way it would from a row on the worklist.',
          style: Theme.of(context).textTheme.bodyMedium,
        ),
        const SizedBox(height: 16),
        TextField(
          controller: controller,
          textInputAction: TextInputAction.done,
          onSubmitted: (_) => onSubmit(),
          decoration: const InputDecoration(
            labelText: 'NIC or patient code',
            hintText: 'e.g. 200012345678 or PT-7K4M2Q',
            border: OutlineInputBorder(),
          ),
        ),
        if (error != null) ...[
          const SizedBox(height: 8),
          Text(error!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
        ],
        const SizedBox(height: 16),
        FilledButton(
          onPressed: starting ? null : onSubmit,
          child: Text(starting ? 'Starting…' : 'Suggest a bed'),
        ),
      ],
    );
  }
}

class _WorkflowView extends StatelessWidget {
  const _WorkflowView({
    required this.workflow,
    required this.onRetry,
    required this.assigning,
    required this.role,
    required this.onConfirm,
  });

  final AsyncData<BedWorkflowSummary>? workflow;
  final VoidCallback onRetry;
  final bool assigning;
  final PrincipalRole? role;
  final Future<void> Function(BedWorkflowSummary summary, SuggestedBed bed) onConfirm;

  @override
  Widget build(BuildContext context) {
    final state = workflow;
    if (state == null) {
      return const Center(child: CircularProgressIndicator());
    }

    return AsyncView<BedWorkflowSummary>(
      state: state,
      onRetry: onRetry,
      builder: (context, summary) {
        if (summary.status == BedWorkflowStatus.running) {
          return const Center(
            child: Padding(
              padding: EdgeInsets.only(top: 48),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  CircularProgressIndicator(),
                  SizedBox(height: 16),
                  Text('The agent is working on this — usually a few seconds.'),
                ],
              ),
            ),
          );
        }

        final theme = Theme.of(context);
        final patient = summary.patient;
        final blocker = summary.blocker;

        final beds = [
          if (summary.best != null) (summary.best!, 'Suggested'),
          ...(summary.alternatives ?? []).map((bed) => (bed, 'Alternative')),
        ];

        return ListView(
          children: [
            if (patient != null) _PatientCard(patient: patient),
            const SizedBox(height: 12),
            if (blocker != null)
              Container(
                padding: const EdgeInsets.all(16),
                decoration: BoxDecoration(
                  color: theme.colorScheme.errorContainer,
                  borderRadius: BorderRadius.circular(AppTheme.radiusS),
                ),
                child: Text(
                  blocker.message ?? 'The agent could not suggest a bed.',
                  style: TextStyle(color: theme.colorScheme.onErrorContainer),
                ),
              )
            else
              ...beds.map((entry) {
                final (bed, label) = entry;
                final hidden = bed.requiresDutyManager && role != PrincipalRole.dutyManager;

                return Card(
                  margin: const EdgeInsets.only(bottom: 12),
                  child: Padding(
                    padding: const EdgeInsets.all(16),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(label, style: theme.textTheme.labelMedium),
                        const SizedBox(height: 4),
                        Text(
                          '${bed.wardName ?? 'Ward'} · ${bed.bedNumber ?? '—'}',
                          style: theme.textTheme.titleMedium,
                        ),
                        if (bed.isDowngrade)
                          Text(
                            'A downgrade from the requested level',
                            style: theme.textTheme.bodySmall,
                          ),
                        if (bed.rationale != null) ...[
                          const SizedBox(height: 4),
                          Text(bed.rationale!, style: theme.textTheme.bodySmall),
                        ],
                        if (!hidden) ...[
                          const SizedBox(height: 12),
                          FilledButton(
                            onPressed: assigning ? null : () => onConfirm(summary, bed),
                            child: Text(assigning ? 'Assigning…' : 'Use this bed'),
                          ),
                        ],
                      ],
                    ),
                  ),
                );
              }),
          ],
        );
      },
    );
  }
}

class _PatientCard extends StatelessWidget {
  const _PatientCard({required this.patient});

  final BedSuggestionPatient patient;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(patient.fullName ?? 'Unknown patient', style: theme.textTheme.titleMedium),
            if (patient.patientCode != null)
              Text(patient.patientCode!, style: theme.textTheme.bodySmall),
            const SizedBox(height: 8),
            Wrap(
              spacing: 12,
              runSpacing: 4,
              children: [
                if (patient.gender != null) Text(_genderLabel(patient.gender!)),
                if (patient.admissionCategory != null)
                  Text(_categoryLabel(patient.admissionCategory!)),
                if (patient.urgency != null) Text(_urgencyLabel(patient.urgency!)),
              ],
            ),
          ],
        ),
      ),
    );
  }

  static String _genderLabel(Gender value) => switch (value) {
        Gender.male => 'Male',
        Gender.female => 'Female',
        Gender.other => 'Other',
        Gender.unknown => 'Unknown',
        Gender.$unknown => 'Unknown',
      };

  static String _categoryLabel(AdmissionCategory value) => switch (value) {
        AdmissionCategory.icu => 'ICU',
        AdmissionCategory.hdu => 'HDU',
        AdmissionCategory.inpatient => 'Inpatient',
        AdmissionCategory.dayCase => 'Day case',
        AdmissionCategory.outpatient => 'Outpatient',
        AdmissionCategory.$unknown => 'Care level unknown',
      };

  static String _urgencyLabel(AdmissionUrgency value) => switch (value) {
        AdmissionUrgency.emergency => 'Emergency',
        AdmissionUrgency.urgent => 'Urgent',
        AdmissionUrgency.routine => 'Routine',
        AdmissionUrgency.$unknown => 'Urgency unknown',
      };
}
