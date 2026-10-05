import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../../core/theme/app_theme.dart';
import '../../../services/api_client/models/cancellation_request_status.dart';
import '../../../services/api_client/models/my_call_tracking.dart';
import '../../patient/patient_routes.dart';
import '../emergency_routes.dart';
import '../models/patient_call_text.dart';
import '../state/patient_emergency_controller.dart';
import '../widgets/dialog_actions.dart';
import '../widgets/maps_launcher.dart';

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
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) {
        context.read<PatientEmergencyController>().watch(widget.callId);
      }
    });
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
    final controller = context.read<PatientEmergencyController>();
    final reason = await showDialog<String>(
      context: context,
      builder: (_) => _CancelDialog(needsReview: controller.cancelNeedsReview),
    );
    if (!mounted || reason == null) return;
    await controller.cancel(widget.callId, reason);
  }

  @override
  Widget build(BuildContext context) {
    final controller = context.watch<PatientEmergencyController>();
    final tracking = controller.tracking;
    final theme = Theme.of(context);
    final stage = tracking == null ? null : trackingStage(tracking);
    final cancellation = cancellationText(tracking?.cancellationRequestStatus);

    return Scaffold(
      appBar: AppBar(
        title: const Text('Ambulance request'),
        // A notification tap opens this screen on its own, with nothing under it.
        leading: context.canPop()
            ? null
            : IconButton(
                tooltip: 'Home',
                icon: const Icon(Icons.arrow_back),
                onPressed: () => context.go(PatientPaths.home),
              ),
      ),
      body: RefreshIndicator(
        onRefresh: () => controller.refreshTracking(widget.callId),
        child: ListView(
          physics: const AlwaysScrollableScrollPhysics(),
          padding: const EdgeInsets.all(AppTheme.gutter),
          children: [
            Text(
              stage?.title ?? 'Checking your request…',
              style: theme.textTheme.headlineSmall,
            ),
            if (stage != null && stage.message.isNotEmpty) ...[
              const SizedBox(height: 4),
              Text(stage.message, style: theme.textTheme.bodyLarge),
            ],
            if (tracking != null && tracking.ambulanceRegistration != null) ...[
              const SizedBox(height: 16),
              _AmbulanceCard(tracking: tracking),
            ],
            if (tracking?.updatedAt != null) ...[
              const SizedBox(height: 8),
              Text(
                'Updated ${MaterialLocalizations.of(context).formatTimeOfDay(TimeOfDay.fromDateTime(tracking!.updatedAt!.toLocal()))}',
                style: theme.textTheme.bodySmall,
              ),
            ],
            if (cancellation != null) ...[
              const SizedBox(height: 16),
              Card(
                child: Padding(
                  padding: const EdgeInsets.all(16),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(cancellation),
                      if (tracking?.cancellationRequestStatus ==
                              CancellationRequestStatus.rejected &&
                          tracking?.cancellationReviewNotes
                                  ?.trim()
                                  .isNotEmpty ==
                              true) ...[
                        const SizedBox(height: 8),
                        Text(
                          'Their note: ${tracking!.cancellationReviewNotes}',
                          style: theme.textTheme.bodyMedium?.copyWith(
                            fontStyle: FontStyle.italic,
                          ),
                        ),
                      ],
                    ],
                  ),
                ),
              ),
            ],
            if (controller.error != null) ...[
              const SizedBox(height: 12),
              Text(
                controller.error!.message,
                style: TextStyle(color: theme.colorScheme.error),
              ),
            ],
            if (controller.canCancel) ...[
              const SizedBox(height: 24),
              OutlinedButton(
                onPressed: controller.acting ? null : _cancel,
                child: Text(
                  controller.cancelNeedsReview
                      ? 'Ask to cancel'
                      : 'Cancel request',
                ),
              ),
            ],
            if (tracking != null && isOpenCall(tracking.callStatus)) ...[
              const SizedBox(height: 8),
              TextButton(
                onPressed: () => context.push(EmergencyPaths.patientReport),
                child: const Text(
                  'A different emergency? Request another ambulance',
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

class _AmbulanceCard extends StatelessWidget {
  const _AmbulanceCard({required this.tracking});

  final MyCallTracking tracking;

  @override
  Widget build(BuildContext context) {
    final minutes = tracking.estimatedMinutesToArrival;
    final distance = tracking.ambulanceDistanceKm;
    final latitude = tracking.ambulanceLatitude;
    final longitude = tracking.ambulanceLongitude;
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'Ambulance ${tracking.ambulanceRegistration}',
              style: Theme.of(context).textTheme.titleMedium,
            ),
            if (minutes != null) ...[
              const SizedBox(height: 4),
              Text('About $minutes min away'),
            ],
            if (distance != null)
              Text('${distance.toStringAsFixed(1)} km away'),
            if (tracking.ambulanceLocationIsStale == true)
              Text(
                'Its position has not updated for a few minutes, so this may be out of date.',
                style: TextStyle(color: Theme.of(context).colorScheme.error),
              ),
            if (latitude != null && longitude != null) ...[
              const SizedBox(height: 8),
              OutlinedButton.icon(
                onPressed: () => openInMaps(
                  context,
                  'https://www.google.com/maps/search/?api=1&query=$latitude,$longitude',
                ),
                icon: const Icon(Icons.map_outlined),
                label: const Text('See the ambulance on a map'),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

class _CancelDialog extends StatefulWidget {
  const _CancelDialog({required this.needsReview});

  final bool needsReview;

  @override
  State<_CancelDialog> createState() => _CancelDialogState();
}

class _CancelDialogState extends State<_CancelDialog> {
  final _reason = TextEditingController();
  bool _triedEmpty = false;

  @override
  void dispose() {
    _reason.dispose();
    super.dispose();
  }

  void _submit() {
    final reason = _reason.text.trim();
    if (reason.isEmpty) {
      setState(() => _triedEmpty = true);
      return;
    }
    Navigator.pop(context, reason);
  }

  @override
  Widget build(BuildContext context) => AlertDialog(
    title: Text(
      widget.needsReview ? 'Ask to cancel?' : 'Cancel ambulance request?',
    ),
    content: Column(
      mainAxisSize: MainAxisSize.min,
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        if (widget.needsReview)
          const Padding(
            padding: EdgeInsets.only(bottom: 8),
            child: Text(
              'An ambulance is already on its way. The duty manager decides, and it keeps coming until they agree.',
            ),
          ),
        TextField(
          controller: _reason,
          maxLength: 500,
          autofocus: true,
          decoration: InputDecoration(
            labelText: 'Why do you want to cancel?',
            errorText: _triedEmpty ? 'Please give a reason.' : null,
          ),
          onChanged: (_) {
            if (_triedEmpty) setState(() => _triedEmpty = false);
          },
        ),
      ],
    ),
    actions: stackedDialogActions(
      confirm: FilledButton(
        onPressed: _submit,
        child: Text(widget.needsReview ? 'Send request' : 'Cancel request'),
      ),
      back: TextButton(
        onPressed: () => Navigator.pop(context),
        child: const Text('Keep request'),
      ),
    ),
  );
}
