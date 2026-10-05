import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../../core/auth/auth_controller.dart';
import '../../../core/network/api_exception.dart';
import '../../../core/notifications/notification_bell.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/widgets/async_view.dart';
import '../emergency_routes.dart';
import '../models/run_step.dart';
import '../services/crew_location_reporter.dart';
import '../state/my_run_controller.dart';
import '../widgets/crew_location_lifecycle.dart';
import '../widgets/maps_launcher.dart';
import '../widgets/phone_call.dart';
import '../widgets/run_card.dart';
import '../widgets/run_ended_view.dart';
import '../widgets/run_prompts.dart';

class MyRunScreen extends StatefulWidget {
  const MyRunScreen({super.key});

  @override
  State<MyRunScreen> createState() => _MyRunScreenState();
}

class _MyRunScreenState extends State<MyRunScreen> with WidgetsBindingObserver {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) context.read<MyRunController>().startPolling();
    });
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed) {
      context.read<MyRunController>().load(showLoading: false);
    }
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final reporter = context.watch<CrewLocationReporter>();
    final controller = context.watch<MyRunController>();
    final locationNotice = _locationNotice(reporter.state);
    final error = controller.actionError;
    final scheme = Theme.of(context).colorScheme;

    return CrewLocationLifecycle(
      reporter: reporter,
      child: Scaffold(
        appBar: AppBar(
          title: const Text('My run'),
          actions: [
            const NotificationBell(),
            IconButton(
              tooltip: 'Past runs',
              icon: const Icon(Icons.history),
              onPressed: () => context.push(EmergencyPaths.history),
            ),
            PopupMenuButton<void>(
              tooltip: 'More',
              itemBuilder: (_) => [
                PopupMenuItem(
                  onTap: () => _signOut(context, controller),
                  child: const Text('Sign out'),
                ),
              ],
            ),
          ],
        ),
        body: Column(
          children: [
            if (locationNotice != null)
              _RunNotice(
                icon: reporter.state == CrewLocationReportingState.reporting
                    ? Icons.my_location
                    : Icons.location_off_outlined,
                color: reporter.state == CrewLocationReportingState.reporting
                    ? scheme.primary
                    : scheme.warning,
                text: locationNotice,
                action: switch (_locationFixLabel(reporter.state)) {
                  final label? => TextButton(
                    onPressed: reporter.fixAccess,
                    child: Text(label),
                  ),
                  null => null,
                },
              ),
            if (error != null)
              _RunNotice(
                icon: Icons.error_outline,
                color: scheme.error,
                text: _errorText(error),
                action: TextButton(
                  onPressed: controller.clearActionError,
                  child: const Text('Dismiss'),
                ),
              ),
            Expanded(
              child: AsyncView(
                state: controller.state,
                onRetry: controller.load,
                builder: (context, run) => switch ((run, controller.endedRun)) {
                  (final run?, _) => RunCard(
                    run: run,
                    busy: controller.busy,
                    onStep: () =>
                        _step(context, controller, run.status?.nextStep),
                    onDecline: () => _decline(context, controller),
                    onNavigate: () => _navigate(context, controller),
                    onEndAtScene: () => _endAtScene(context, controller),
                    onCallCaller: (phone) => callPhone(context, phone),
                  ),
                  (null, final ended?) => RunEndedView(
                    run: ended,
                    onDismiss: controller.dismissEndedRun,
                  ),
                  (null, null) => RefreshableMessage(
                    onRefresh: () => controller.load(showLoading: false),
                    child: controller.onAmbulance
                        ? const EmptyView(
                            icon: Icons.local_hospital_outlined,
                            title: 'No run right now',
                            message:
                                'When the duty manager sends you to a call it will appear here.',
                          )
                        : const EmptyView(
                            icon: Icons.person_off_outlined,
                            title: 'You are not on an ambulance',
                            message:
                                'Runs only reach crew on an ambulance. Ask the duty manager to add you to one.',
                          ),
                  ),
                },
              ),
            ),
          ],
        ),
      ),
    );
  }

  Future<void> _step(
    BuildContext context,
    MyRunController controller,
    RunStep? step,
  ) async {
    switch (step) {
      case RunStep.acknowledge:
        await controller.acknowledge();
      case RunStep.handOver:
        final details = await askHandoverDetails(context);
        if (details != null) {
          await controller.handOver(
            notes: details.notes,
            patientCondition: details.patientCondition,
          );
        }
      case RunStep.arrivedAtScene || RunStep.leaveForHospital:
        final confirmed = await confirmRunStep(
          context,
          title: step == RunStep.arrivedAtScene
              ? 'Have you reached the patient?'
              : 'Is the patient on board?',
          confirmLabel: step == RunStep.arrivedAtScene
              ? 'Yes, at the scene'
              : 'Yes, going to hospital',
        );
        if (confirmed) await controller.advance();
      case RunStep.startDriving:
        await controller.advance();
      case null:
        break;
    }
  }

  Future<void> _signOut(
    BuildContext context,
    MyRunController controller,
  ) async {
    final auth = context.read<AuthController>();
    if (controller.state.valueOrNull != null) {
      final confirmed = await confirmRunStep(
        context,
        title: 'Sign out during a run?',
        message:
            'This phone will stop sharing the ambulance location. Your crew mate can carry on from their phone.',
        confirmLabel: 'Sign out',
      );
      if (!confirmed) return;
    }
    await auth.signOut();
  }

  Future<void> _endAtScene(
    BuildContext context,
    MyRunController controller,
  ) async {
    final result = await askSceneOutcome(context);
    if (result != null) {
      await controller.closeAtScene(result.outcome, notes: result.notes);
    }
  }

  // A 409 here means someone else changed the run first, often a crew mate
  // tapping the same button; the screen has already re-read the run.
  String _errorText(ApiException error) => error.isConflict
      ? 'Someone else changed this run first, so your tap was not saved. The screen now shows the latest.'
      : error.message;

  Future<void> _decline(
    BuildContext context,
    MyRunController controller,
  ) async {
    final reason = await askDeclineReason(context);
    if (reason != null) await controller.decline(reason);
  }

  Future<void> _navigate(
    BuildContext context,
    MyRunController controller,
  ) async {
    final target = await controller.navigationTarget();
    final url = target?.googleMapsUrl;
    if (url != null && context.mounted) await openInMaps(context, url);
  }

  String? _locationNotice(CrewLocationReportingState state) => switch (state) {
    CrewLocationReportingState.reporting =>
      'Sharing your location, even when the phone is locked or Google Maps is open.',
    CrewLocationReportingState.stopped => null,
    CrewLocationReportingState.approximateOnly =>
      'Only approximate location is allowed. Dispatch needs precise location to send the nearest ambulance.',
    CrewLocationReportingState.permissionDenied =>
      'Location permission is needed to report this ambulance position.',
    CrewLocationReportingState.permissionPermanentlyDenied =>
      'Enable location permission in device settings to report this ambulance position.',
    CrewLocationReportingState.unavailable =>
      'Location is turned off on this phone.',
    CrewLocationReportingState.failed =>
      'Could not update ambulance location. Retrying shortly.',
  };

  String? _locationFixLabel(CrewLocationReportingState state) =>
      switch (state) {
        CrewLocationReportingState.permissionDenied => 'Allow',
        CrewLocationReportingState.approximateOnly ||
        CrewLocationReportingState.permissionPermanentlyDenied =>
          'Open settings',
        CrewLocationReportingState.unavailable => 'Turn on location',
        _ => null,
      };
}

class _RunNotice extends StatelessWidget {
  const _RunNotice({
    required this.icon,
    required this.color,
    required this.text,
    this.action,
  });

  final IconData icon;
  final Color color;
  final String text;
  final Widget? action;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Container(
      margin: const EdgeInsets.fromLTRB(AppTheme.gutter, 4, AppTheme.gutter, 8),
      padding: EdgeInsets.fromLTRB(14, 10, action == null ? 14 : 4, 10),
      decoration: BoxDecoration(
        color: Color.alphaBlend(
          color.withValues(alpha: 0.08),
          theme.colorScheme.surface,
        ),
        borderRadius: BorderRadius.circular(AppTheme.radiusM),
        border: Border.all(color: color.withValues(alpha: 0.28)),
      ),
      child: Row(
        children: [
          Icon(icon, size: 20, color: color),
          const SizedBox(width: 12),
          Expanded(
            child: Text(
              text,
              style: theme.textTheme.bodySmall?.copyWith(
                color: theme.colorScheme.onSurface,
              ),
            ),
          ),
          if (action != null) ...[const SizedBox(width: 4), action!],
        ],
      ),
    );
  }
}
