import 'dart:async';

import 'package:flutter/material.dart';

import '../../../core/theme/app_theme.dart';
import '../../../services/api_client/models/dispatch_detail.dart';
import '../models/run_step.dart';
import 'scene_card.dart';

class RunCard extends StatelessWidget {
  const RunCard({
    super.key,
    required this.run,
    required this.busy,
    required this.onStep,
    required this.onDecline,
    required this.onNavigate,
    required this.onEndAtScene,
    required this.onWriteHandoverNotes,
    this.hasHandoverNotes = false,
  });

  final DispatchDetail run;
  final bool busy;
  final VoidCallback onStep;
  final VoidCallback onDecline;
  final VoidCallback onNavigate;
  final VoidCallback onEndAtScene;
  final VoidCallback onWriteHandoverNotes;
  final bool hasHandoverNotes;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final status = run.status;
    final step = status?.nextStep;

    return ListView(
      padding: const EdgeInsets.all(AppTheme.gutter),
      children: [
        Text(status?.crewLabel ?? '', style: theme.textTheme.headlineSmall),
        const SizedBox(height: 4),
        Text(
          'Priority: ${run.callPriority?.name ?? 'unknown'}',
          style: theme.textTheme.bodyLarge?.copyWith(
            color: theme.colorScheme.onSurfaceVariant,
          ),
        ),
        const SizedBox(height: 16),
        Card(
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                _Row(label: 'Ambulance', value: run.ambulanceRegistration),
                _Row(label: 'Crew on board', value: run.crewCount?.toString()),
                _Row(label: 'Going to ward', value: run.destinationWardName),
              ],
            ),
          ),
        ),
        const SizedBox(height: 12),
        SceneCard(run: run),
        const SizedBox(height: 24),
        if (step != null)
          _StepButton(step: step, busy: busy, onPressed: onStep),
        if (status?.canWriteHandoverNotes ?? false) ...[
          const SizedBox(height: 12),
          OutlinedButton.icon(
            onPressed: busy ? null : onWriteHandoverNotes,
            icon: const Icon(Icons.edit_note),
            label: Text(
              hasHandoverNotes ? 'Edit handover notes' : 'Write handover notes',
            ),
            style: OutlinedButton.styleFrom(
              minimumSize: const Size.fromHeight(52),
            ),
          ),
        ],
        if (status?.canEndAtScene ?? false) ...[
          const SizedBox(height: 12),
          OutlinedButton(
            onPressed: busy ? null : onEndAtScene,
            style: OutlinedButton.styleFrom(
              minimumSize: const Size.fromHeight(52),
            ),
            child: const Text('Finish without going to hospital'),
          ),
        ],
        if (status?.canNavigate ?? false) ...[
          const SizedBox(height: 12),
          OutlinedButton.icon(
            onPressed: busy ? null : onNavigate,
            icon: const Icon(Icons.navigation_outlined),
            label: Text(status!.navigationLabel),
            style: OutlinedButton.styleFrom(
              minimumSize: const Size.fromHeight(52),
            ),
          ),
        ],
        if (status?.canDecline ?? false) ...[
          const SizedBox(height: 12),
          TextButton(
            onPressed: busy ? null : onDecline,
            child: const Text('I cannot take this run'),
          ),
        ],
      ],
    );
  }
}

class _StepButton extends StatefulWidget {
  const _StepButton({
    required this.step,
    required this.busy,
    required this.onPressed,
  });

  final RunStep step;
  final bool busy;
  final VoidCallback onPressed;

  @override
  State<_StepButton> createState() => _StepButtonState();
}

class _StepButtonState extends State<_StepButton> {
  static const _confirmWindow = Duration(seconds: 4);
  static const _amber = Color(0xFFF5A524);
  static const _onAmber = Color(0xFF231600);

  Timer? _reset;
  bool _armed = false;

  @override
  void didUpdateWidget(_StepButton oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.step != widget.step || widget.busy) _disarm();
  }

  @override
  void dispose() {
    _reset?.cancel();
    super.dispose();
  }

  void _disarm() {
    _reset?.cancel();
    _armed = false;
  }

  void _press() {
    if (!widget.step.needsConfirmation || _armed) {
      setState(_disarm);
      widget.onPressed();
      return;
    }
    setState(() => _armed = true);
    _reset = Timer(_confirmWindow, () => setState(_disarm));
  }

  @override
  Widget build(BuildContext context) {
    final onPressed = widget.busy ? null : _press;
    return Semantics(
      button: true,
      enabled: onPressed != null,
      liveRegion: _armed,
      excludeSemantics: true,
      label: _armed
          ? 'Tap again to confirm: ${widget.step.label}'
          : widget.step.label,
      onTap: onPressed,
      child: FilledButton(
        onPressed: onPressed,
        style: FilledButton.styleFrom(
          minimumSize: const Size.fromHeight(56),
          backgroundColor: _armed ? _amber : null,
          foregroundColor: _armed ? _onAmber : null,
        ),
        child: widget.busy
            ? const SizedBox.square(
                dimension: 22,
                child: CircularProgressIndicator(strokeWidth: 2),
              )
            : Text(_armed ? 'Tap again to confirm' : widget.step.label),
      ),
    );
  }
}

class _Row extends StatelessWidget {
  const _Row({required this.label, required this.value});

  final String label;
  final String? value;

  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.symmetric(vertical: 4),
    child: Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      children: [
        Text(label),
        Flexible(child: Text(value ?? 'Not set', textAlign: TextAlign.end)),
      ],
    ),
  );
}
