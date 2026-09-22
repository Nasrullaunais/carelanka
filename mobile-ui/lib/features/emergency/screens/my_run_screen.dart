import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../../core/widgets/async_view.dart';
import '../emergency_routes.dart';
import '../models/run_step.dart';
import '../services/crew_location_reporter.dart';
import '../state/my_run_controller.dart';
import '../widgets/crew_location_lifecycle.dart';
import '../widgets/maps_launcher.dart';
import '../widgets/run_card.dart';
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
    context.read<MyRunController>().startPolling();
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed) context.read<MyRunController>().load(showLoading: false);
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

    return CrewLocationLifecycle(
      reporter: reporter,
      child: Scaffold(
        appBar: AppBar(
          title: const Text('My run'),
          actions: [
            IconButton(
              tooltip: 'Past runs',
              icon: const Icon(Icons.history),
              onPressed: () => context.push(EmergencyPaths.history),
            ),
          ],
        ),
        body: Column(
          children: [
            if (locationNotice != null && controller.state.valueOrNull != null)
              MaterialBanner(content: Text(locationNotice), actions: const [SizedBox.shrink()]),
            if (error != null)
              MaterialBanner(
                content: Text(error.message),
                actions: [TextButton(onPressed: controller.clearActionError, child: const Text('Dismiss'))],
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
                          message: 'When the duty manager sends you to a call it will appear here.',
                        ),
                      )
                    : RunCard(
                        run: run,
                        busy: controller.busy,
                        onStep: () => _step(context, controller, run.status?.nextStep),
                        onDecline: () => _decline(context, controller),
                        onNavigate: () => _navigate(context, controller),
                      ),
              ),
            ),
          ],
        ),
      ),
    );
  }

  Future<void> _step(BuildContext context, MyRunController controller, RunStep? step) async {
    switch (step) {
      case RunStep.acknowledge:
        await controller.acknowledge();
      case RunStep.handOver:
        final details = await askHandoverDetails(context);
        if (details != null) await controller.handOver(notes: details.notes, patientCondition: details.patientCondition);
      case null:
        break;
      default:
        await controller.advance();
    }
  }

  Future<void> _decline(BuildContext context, MyRunController controller) async {
    final reason = await askDeclineReason(context);
    if (reason != null) await controller.decline(reason);
  }

  Future<void> _navigate(BuildContext context, MyRunController controller) async {
    final target = await controller.navigationTarget();
    final url = target?.googleMapsUrl;
    if (url != null && context.mounted) await openInMaps(context, url);
  }

  String? _locationNotice(CrewLocationReportingState state) => switch (state) {
        CrewLocationReportingState.reporting || CrewLocationReportingState.stopped => null,
        CrewLocationReportingState.permissionDenied => 'Location permission is needed to report this ambulance position.',
        CrewLocationReportingState.permissionPermanentlyDenied => 'Enable location permission in device settings to report this ambulance position.',
        CrewLocationReportingState.unavailable => 'Location services are unavailable on this device.',
        CrewLocationReportingState.failed => 'Location reporting stopped. Reopen this screen to try again.',
      };
}
