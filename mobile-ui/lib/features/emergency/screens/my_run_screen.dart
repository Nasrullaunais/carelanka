import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../../core/notifications/notification_bell.dart';
import '../../../core/widgets/async_view.dart';
import '../emergency_routes.dart';
import '../models/run_step.dart';
import '../services/crew_location_reporter.dart';
import '../state/my_run_controller.dart';
import '../widgets/crew_location_lifecycle.dart';
import '../widgets/maps_launcher.dart';
import '../widgets/run_card.dart';
import '../widgets/run_ending_panel.dart';
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
    final error = controller.actionError;
    final liveRun = controller.state.valueOrNull;
    final locationNotice = _locationNotice(
      reporter.state,
      onRun: liveRun != null,
    );

    return CrewLocationLifecycle(
      reporter: reporter,
      mode: liveRun == null
          ? const CrewReportingMode.foreground()
          : CrewReportingMode.run(
              liveRun.ambulanceRegistration ?? 'your ambulance',
            ),
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
          ],
        ),
        body: Column(
          children: [
            if (locationNotice != null)
              MaterialBanner(
                content: Text(locationNotice),
                actions: [
                  if (_locationFixLabel(reporter.state) case final label?)
                    TextButton(
                      onPressed: reporter.fixAccess,
                      child: Text(label),
                    )
                  else
                    const SizedBox.shrink(),
                ],
              ),
            if (error != null)
              MaterialBanner(
                content: Text(
                  controller.canRetry
                      ? 'No connection — this step was not saved.'
                      : error.message,
                ),
                actions: [
                  if (controller.canRetry)
                    TextButton(
                      onPressed: controller.busy
                          ? null
                          : () => _afterMove(
                              context,
                              controller,
                              controller.retry(),
                            ),
                      child: const Text('Try again'),
                    ),
                  TextButton(
                    onPressed: controller.clearActionError,
                    child: const Text('Dismiss'),
                  ),
                ],
              ),
            if (controller.ending case final ending?)
              RunEndingPanel(
                ending: ending,
                onDismiss: controller.dismissEnding,
              ),
            Expanded(
              child: AsyncView(
                state: controller.state,
                onRetry: controller.load,
                builder: (context, run) => run == null
                    ? RefreshableMessage(
                        onRefresh: () => controller.load(showLoading: false),
                        child: const EmptyView(
                          icon: Icons.local_hospital_outlined,
                          title: 'No run right now',
                          message:
                              'When the duty manager sends you to a call it will appear here.',
                        ),
                      )
                    : RunCard(
                        run: run,
                        busy: controller.busy,
                        onStep: () =>
                            _step(context, controller, run.status?.nextStep),
                        onDecline: () => _decline(context, controller),
                        onNavigate: () => _navigate(context, controller),
                        onEndAtScene: () => _endAtScene(context, controller),
                        hasHandoverNotes: !controller.handoverDraft.isEmpty,
                        onWriteHandoverNotes: () =>
                            _writeHandoverNotes(context, controller),
                      ),
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
        final details = await askHandoverDetails(
          context,
          mode: HandoverSheetMode.handOver,
          initial: controller.handoverDraft,
          onChanged: controller.saveHandoverDraft,
        );
        if (details != null) {
          await controller.handOver(
            notes: details.notesOrNull,
            patientCondition: details.patientConditionOrNull,
          );
        }
      case null:
        break;
      default:
        await _afterMove(context, controller, controller.advance());
    }
  }

  Future<void> _afterMove(
    BuildContext context,
    MyRunController controller,
    Future<bool> move,
  ) async {
    final moved = await move;
    final opensMaps =
        controller.state.valueOrNull?.status?.opensNavigationOnEntry ?? false;
    if (moved && opensMaps && context.mounted) {
      await _navigate(context, controller);
    }
  }

  Future<void> _writeHandoverNotes(
    BuildContext context,
    MyRunController controller,
  ) => askHandoverDetails(
    context,
    mode: HandoverSheetMode.draft,
    initial: controller.handoverDraft,
    onChanged: controller.saveHandoverDraft,
  );

  Future<void> _endAtScene(
    BuildContext context,
    MyRunController controller,
  ) async {
    final finish = await askSceneOutcome(context);
    if (finish != null) {
      await controller.endAtScene(finish.outcome, notes: finish.notes);
    }
  }

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

  String? _locationNotice(
    CrewLocationReportingState state, {
    required bool onRun,
  }) => switch (state) {
    CrewLocationReportingState.reporting =>
      onRun
          ? 'Sharing your ambulance location until this run ends.'
          : 'Sharing your assigned ambulance location while this screen is open.',
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
