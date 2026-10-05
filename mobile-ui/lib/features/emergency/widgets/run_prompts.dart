import 'package:flutter/material.dart';

import '../../../services/api_client/models/emergency_call_outcome.dart';
import '../models/run_step.dart';
import 'dialog_actions.dart';

Future<String?> askDeclineReason(BuildContext context) => showDialog<String>(
  context: context,
  builder: (_) => const _DeclineDialog(),
);

typedef HandoverDetails = ({String? notes, String? patientCondition});

Future<HandoverDetails?> askHandoverDetails(BuildContext context) =>
    showModalBottomSheet<HandoverDetails>(
      context: context,
      isScrollControlled: true,
      builder: (_) => const _HandoverSheet(),
    );

typedef SceneOutcome = ({EmergencyCallOutcome outcome, String? notes});

Future<SceneOutcome?> askSceneOutcome(BuildContext context) =>
    showModalBottomSheet<SceneOutcome>(
      context: context,
      isScrollControlled: true,
      builder: (_) => const _SceneOutcomeSheet(),
    );

/// A second tap for steps that cannot be undone, since gloves and a moving
/// vehicle make a wrong tap easy.
Future<bool> confirmRunStep(
  BuildContext context, {
  required String title,
  required String confirmLabel,
  String message = 'This cannot be undone.',
}) async =>
    await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: Text(title),
        content: Text(message),
        actions: stackedDialogActions(
          confirm: FilledButton(
            onPressed: () => Navigator.pop(dialogContext, true),
            child: Text(confirmLabel),
          ),
          back: TextButton(
            onPressed: () => Navigator.pop(dialogContext, false),
            child: const Text('Back'),
          ),
        ),
      ),
    ) ??
    false;

String? _blankToNull(String text) => text.trim().isEmpty ? null : text.trim();

class _DeclineDialog extends StatefulWidget {
  const _DeclineDialog();

  @override
  State<_DeclineDialog> createState() => _DeclineDialogState();
}

class _DeclineDialogState extends State<_DeclineDialog> {
  final _reason = TextEditingController();

  @override
  void dispose() {
    _reason.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => AlertDialog(
    title: const Text('Why can you not take this run?'),
    content: TextField(
      controller: _reason,
      maxLength: 500,
      maxLines: 3,
      autofocus: true,
      decoration: const InputDecoration(
        hintText: 'For example: vehicle problem',
      ),
      onChanged: (_) => setState(() {}),
    ),
    actions: stackedDialogActions(
      confirm: FilledButton(
        onPressed: _reason.text.trim().isEmpty
            ? null
            : () => Navigator.pop(context, _reason.text.trim()),
        child: const Text('Decline run'),
      ),
      back: TextButton(
        onPressed: () => Navigator.pop(context),
        child: const Text('Back'),
      ),
    ),
  );
}

class _HandoverSheet extends StatefulWidget {
  const _HandoverSheet();

  @override
  State<_HandoverSheet> createState() => _HandoverSheetState();
}

class _HandoverSheetState extends State<_HandoverSheet> {
  final _condition = TextEditingController();
  final _notes = TextEditingController();

  @override
  void dispose() {
    _condition.dispose();
    _notes.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => Padding(
    padding: EdgeInsets.fromLTRB(
      20,
      20,
      20,
      20 + MediaQuery.viewInsetsOf(context).bottom,
    ),
    child: Column(
      mainAxisSize: MainAxisSize.min,
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          'Hand over the patient',
          style: Theme.of(context).textTheme.titleLarge,
        ),
        const SizedBox(height: 16),
        TextField(
          controller: _condition,
          maxLength: 500,
          decoration: const InputDecoration(
            labelText: 'Patient condition on arrival (optional)',
          ),
        ),
        const SizedBox(height: 8),
        TextField(
          controller: _notes,
          maxLength: 1000,
          maxLines: 3,
          decoration: const InputDecoration(
            labelText: 'Notes for the hospital team (optional)',
          ),
        ),
        const SizedBox(height: 12),
        SizedBox(
          width: double.infinity,
          child: FilledButton(
            onPressed: () => Navigator.pop(context, (
              notes: _blankToNull(_notes.text),
              patientCondition: _blankToNull(_condition.text),
            )),
            child: const Text('Confirm handover'),
          ),
        ),
      ],
    ),
  );
}

class _SceneOutcomeSheet extends StatefulWidget {
  const _SceneOutcomeSheet();

  @override
  State<_SceneOutcomeSheet> createState() => _SceneOutcomeSheetState();
}

class _SceneOutcomeSheetState extends State<_SceneOutcomeSheet> {
  final _notes = TextEditingController();
  EmergencyCallOutcome? _outcome;

  @override
  void dispose() {
    _notes.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => Padding(
    padding: EdgeInsets.fromLTRB(
      20,
      20,
      20,
      20 + MediaQuery.viewInsetsOf(context).bottom,
    ),
    child: SingleChildScrollView(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            'End without going to hospital',
            style: Theme.of(context).textTheme.titleLarge,
          ),
          const SizedBox(height: 4),
          const Text(
            'The run ends here and the ambulance becomes free for the next call.',
          ),
          const SizedBox(height: 12),
          RadioGroup<EmergencyCallOutcome>(
            groupValue: _outcome,
            onChanged: (value) => setState(() => _outcome = value),
            child: Column(
              children: [
                for (final outcome in sceneOutcomes)
                  RadioListTile<EmergencyCallOutcome>(
                    value: outcome,
                    title: Text(outcome.label),
                    contentPadding: EdgeInsets.zero,
                  ),
              ],
            ),
          ),
          TextField(
            controller: _notes,
            maxLength: 1000,
            maxLines: 3,
            decoration: const InputDecoration(
              labelText: 'Notes for the duty manager (optional)',
            ),
          ),
          const SizedBox(height: 12),
          SizedBox(
            width: double.infinity,
            child: FilledButton(
              onPressed: _outcome == null
                  ? null
                  : () => Navigator.pop(context, (
                      outcome: _outcome!,
                      notes: _blankToNull(_notes.text),
                    )),
              child: const Text('End the run'),
            ),
          ),
        ],
      ),
    ),
  );
}
