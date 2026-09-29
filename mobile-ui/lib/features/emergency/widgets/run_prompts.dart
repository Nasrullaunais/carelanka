import 'package:flutter/material.dart';

import '../../../services/api_client/models/scene_outcome.dart';
import '../models/scene_outcome_labels.dart';

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

typedef SceneFinish = ({SceneOutcome outcome, String? notes});

Future<SceneFinish?> askSceneOutcome(BuildContext context) =>
    showModalBottomSheet<SceneFinish>(
      context: context,
      isScrollControlled: true,
      showDragHandle: true,
      builder: (_) => const _SceneOutcomeSheet(),
    );

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
    actions: [
      TextButton(
        onPressed: () => Navigator.pop(context),
        child: const Text('Back'),
      ),
      FilledButton(
        onPressed: _reason.text.trim().isEmpty
            ? null
            : () => Navigator.pop(context, _reason.text.trim()),
        child: const Text('Decline run'),
      ),
    ],
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
  SceneOutcome? _outcome;

  @override
  void dispose() {
    _notes.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => SingleChildScrollView(
    padding: EdgeInsets.fromLTRB(
      20,
      4,
      20,
      20 + MediaQuery.viewInsetsOf(context).bottom,
    ),
    child: Column(
      mainAxisSize: MainAxisSize.min,
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          'Why is nobody going to hospital?',
          style: Theme.of(context).textTheme.titleLarge,
        ),
        const SizedBox(height: 8),
        RadioGroup<SceneOutcome>(
          groupValue: _outcome,
          onChanged: (value) => setState(() => _outcome = value),
          child: Column(
            children: [
              for (final outcome in SceneOutcome.$valuesDefined)
                RadioListTile<SceneOutcome>(
                  value: outcome,
                  title: Text(outcome.crewLabel),
                  contentPadding: EdgeInsets.zero,
                ),
            ],
          ),
        ),
        const SizedBox(height: 8),
        TextField(
          controller: _notes,
          maxLength: 1000,
          maxLines: 3,
          decoration: const InputDecoration(labelText: 'Notes (optional)'),
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
            child: const Text('Finish run'),
          ),
        ),
      ],
    ),
  );
}
