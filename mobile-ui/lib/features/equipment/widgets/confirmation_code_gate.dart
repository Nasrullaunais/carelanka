import 'package:flutter/material.dart';

import '../../../core/theme/app_theme.dart';
import '../state/confirmation_queue_controller.dart';

class ConfirmationCodeGate extends StatefulWidget {
  const ConfirmationCodeGate({super.key, required this.controller, required this.explanation});

  final ConfirmationQueueController<Object?> controller;
  final String explanation;

  @override
  State<ConfirmationCodeGate> createState() => _ConfirmationCodeGateState();
}

class _ConfirmationCodeGateState extends State<ConfirmationCodeGate> {
  final _code = TextEditingController();

  @override
  void dispose() {
    _code.dispose();
    super.dispose();
  }

  Future<void> _unlock() async {
    final unlocked = await widget.controller.unlock(_code.text);
    if (!unlocked && mounted) _code.clear();
  }

  @override
  Widget build(BuildContext context) {
    final controller = widget.controller;
    final theme = Theme.of(context);

    return ListView(
      padding: const EdgeInsets.all(AppTheme.gutter),
      children: [
        Icon(Icons.lock_outline, size: 40, color: theme.colorScheme.primary),
        const SizedBox(height: 12),
        Text(
          'Enter the confirmation code',
          style: theme.textTheme.titleMedium,
          textAlign: TextAlign.center,
        ),
        const SizedBox(height: 6),
        Text(
          widget.explanation,
          style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.onSurfaceVariant),
          textAlign: TextAlign.center,
        ),
        const SizedBox(height: AppTheme.gutter),
        TextField(
          controller: _code,
          enabled: !controller.unlocking,
          obscureText: true,
          autocorrect: false,
          enableSuggestions: false,
          decoration: InputDecoration(
            labelText: 'Confirmation code',
            errorText: controller.unlockProblem,
          ),
          textInputAction: TextInputAction.done,
          onSubmitted: (_) => _unlock(),
        ),
        const SizedBox(height: AppTheme.gutter),
        FilledButton.icon(
          onPressed: controller.unlocking ? null : _unlock,
          icon: controller.unlocking
              ? const SizedBox(
                  width: 18,
                  height: 18,
                  child: CircularProgressIndicator(strokeWidth: 2),
                )
              : const Icon(Icons.lock_open),
          label: Text(controller.unlocking ? 'Checking…' : 'Unlock'),
          style: FilledButton.styleFrom(minimumSize: const Size.fromHeight(52)),
        ),
      ],
    );
  }
}

class ConfirmationDetail extends StatelessWidget {
  const ConfirmationDetail({super.key, required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Padding(
      padding: const EdgeInsets.only(bottom: 4),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SizedBox(
            width: 92,
            child: Text(
              label,
              style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
            ),
          ),
          Expanded(child: Text(value, style: theme.textTheme.bodyMedium)),
        ],
      ),
    );
  }
}
