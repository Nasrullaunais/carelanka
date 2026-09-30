import 'package:flutter/material.dart';

import '../../../services/api_client/models/scene_outcome.dart';
import '../models/decline_reason.dart';
import '../models/handover_draft.dart';
import '../models/scene_outcome_labels.dart';

Future<String?> askDeclineReason(BuildContext context) => showDialog<String>(
  context: context,
  builder: (_) => const _DeclineDialog(),
);

enum HandoverSheetMode {
  draft('Handover notes', 'Save notes'),
  handOver('Hand over the patient', 'Confirm handover');

  const HandoverSheetMode(this.title, this.confirmLabel);

  final String title;
  final String confirmLabel;
}

/// Every keystroke goes to [onChanged], so closing the sheet loses nothing.
Future<HandoverDraft?> askHandoverDetails(
  BuildContext context, {
  required HandoverSheetMode mode,
  required HandoverDraft initial,
  required ValueChanged<HandoverDraft> onChanged,
}) => showModalBottomSheet<HandoverDraft>(
  context: context,
  isScrollControlled: true,
  builder: (_) =>
      _HandoverSheet(mode: mode, initial: initial, onChanged: onChanged),
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
  final _details = TextEditingController();
  DeclineReason? _reason;

  @override
  void dispose() {
    _details.dispose();
    super.dispose();
  }

  String? get _answer => switch (_reason) {
    null => null,
    final reason when reason.needsDetails => _blankToNull(_details.text),
    final reason => reason.label,
  };

  @override
  Widget build(BuildContext context) => AlertDialog(
    title: const Text('Why can you not take this run?'),
    content: SingleChildScrollView(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          RadioGroup<DeclineReason>(
            groupValue: _reason,
            onChanged: (value) => setState(() => _reason = value),
            child: Column(
              children: [
                for (final reason in DeclineReason.values)
                  RadioListTile<DeclineReason>(
                    value: reason,
                    title: Text(reason.label),
                    contentPadding: EdgeInsets.zero,
                  ),
              ],
            ),
          ),
          if (_reason?.needsDetails ?? false)
            TextField(
              controller: _details,
              maxLength: 500,
              maxLines: 3,
              autofocus: true,
              decoration: const InputDecoration(labelText: 'What happened?'),
              onChanged: (_) => setState(() {}),
            ),
        ],
      ),
    ),
    actions: [
      TextButton(
        onPressed: () => Navigator.pop(context),
        child: const Text('Back'),
      ),
      FilledButton(
        onPressed: _answer == null
            ? null
            : () => Navigator.pop(context, _answer),
        child: const Text('Decline run'),
      ),
    ],
  );
}

class _HandoverSheet extends StatefulWidget {
  const _HandoverSheet({
    required this.mode,
    required this.initial,
    required this.onChanged,
  });

  final HandoverSheetMode mode;
  final HandoverDraft initial;
  final ValueChanged<HandoverDraft> onChanged;

  @override
  State<_HandoverSheet> createState() => _HandoverSheetState();
}

class _HandoverSheetState extends State<_HandoverSheet> {
  late final _condition = TextEditingController(
    text: widget.initial.patientCondition,
  )..addListener(_changed);
  late final _notes = TextEditingController(text: widget.initial.notes)
    ..addListener(_changed);

  HandoverDraft get _draft =>
      HandoverDraft(patientCondition: _condition.text, notes: _notes.text);

  void _changed() => widget.onChanged(_draft);

  @override
  void dispose() {
    _condition.dispose();
    _notes.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => SingleChildScrollView(
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
        Text(widget.mode.title, style: Theme.of(context).textTheme.titleLarge),
        const SizedBox(height: 16),
        TextField(
          controller: _condition,
          maxLength: 500,
          decoration: const InputDecoration(
            labelText: 'Patient condition (optional)',
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
            onPressed: () => Navigator.pop(context, _draft),
            child: Text(widget.mode.confirmLabel),
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
