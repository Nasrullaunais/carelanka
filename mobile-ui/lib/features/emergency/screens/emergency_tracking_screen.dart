import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/theme/app_theme.dart';
import '../../../services/api_client/models/call_status.dart';
import '../state/patient_emergency_controller.dart';

class EmergencyTrackingScreen extends StatefulWidget {
  const EmergencyTrackingScreen({super.key, required this.callId});

  final String callId;

  @override
  State<EmergencyTrackingScreen> createState() =>
      _EmergencyTrackingScreenState();
}

class _EmergencyTrackingScreenState extends State<EmergencyTrackingScreen>
    with WidgetsBindingObserver {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    context.read<PatientEmergencyController>().watch(widget.callId);
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed) {
      context.read<PatientEmergencyController>().resumed(widget.callId);
    }
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    super.dispose();
  }

  Future<void> _cancel() async {
    final reason = TextEditingController();
    final value = await showDialog<String>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Cancel ambulance request?'),
        content: TextField(
          controller: reason,
          maxLength: 500,
          decoration: const InputDecoration(labelText: 'Reason'),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context),
            child: const Text('Keep request'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(context, reason.text),
            child: const Text('Continue'),
          ),
        ],
      ),
    );
    reason.dispose();
    if (!mounted || value == null || value.trim().isEmpty) return;
    final status = context
        .read<PatientEmergencyController>()
        .tracking
        ?.callStatus;
    await context.read<PatientEmergencyController>().cancel(
      widget.callId,
      value,
      dispatched: status != null && status != CallStatus.received,
    );
  }

  @override
  Widget build(BuildContext context) {
    final controller = context.watch<PatientEmergencyController>();
    final tracking = controller.tracking;
    final terminal =
        tracking?.callStatus == CallStatus.completed ||
        tracking?.callStatus == CallStatus.cancelled;
    return Scaffold(
      appBar: AppBar(title: const Text('Ambulance request')),
      body: RefreshIndicator(
        onRefresh: () => controller.refreshTracking(widget.callId),
        child: ListView(
          physics: const AlwaysScrollableScrollPhysics(),
          padding: const EdgeInsets.all(AppTheme.gutter),
          children: [
            Text(
              _statusLabel(tracking?.callStatus),
              style: Theme.of(context).textTheme.headlineSmall,
            ),
            const SizedBox(height: 12),
            if (tracking?.estimatedMinutesToArrival != null)
              Text(
                'Estimated arrival: ${tracking!.estimatedMinutesToArrival} minutes',
              ),
            if (tracking?.ambulanceLocationIsStale == true)
              const Text('The ambulance location has not updated recently.'),
            if (tracking?.updatedAt != null)
              Text(
                'Updated ${MaterialLocalizations.of(context).formatTimeOfDay(TimeOfDay.fromDateTime(tracking!.updatedAt!.toLocal()))}',
              ),
            if (tracking?.cancellationRequestStatus != null) ...[
              const SizedBox(height: 12),
              Text('Cancellation: ${tracking!.cancellationRequestStatus}'),
            ],
            if (controller.error != null) ...[
              const SizedBox(height: 12),
              Text(
                controller.error!.message,
                style: TextStyle(color: Theme.of(context).colorScheme.error),
              ),
            ],
            if (!terminal) ...[
              const SizedBox(height: 24),
              OutlinedButton(
                onPressed: controller.acting ? null : _cancel,
                child: const Text('Cancel request'),
              ),
            ],
          ],
        ),
      ),
    );
  }

  static String _statusLabel(CallStatus? status) => switch (status) {
    CallStatus.received => 'Request received',
    CallStatus.dispatched => 'An ambulance is on the way',
    CallStatus.enRoute => 'Ambulance response in progress',
    CallStatus.completed => 'Response completed',
    CallStatus.cancelled => 'Request cancelled',
    _ => 'Checking your request…',
  };
}
