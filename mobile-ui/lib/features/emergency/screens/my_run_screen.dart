import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../services/crew_location_reporter.dart';
import '../widgets/crew_location_lifecycle.dart';

class MyRunScreen extends StatelessWidget {
  const MyRunScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final reporter = context.watch<CrewLocationReporter>();
    return CrewLocationLifecycle(
      reporter: reporter,
      child: Scaffold(
        appBar: AppBar(title: const Text('My run')),
        body: Center(
          child: Padding(
            padding: const EdgeInsets.all(24),
            child: Text(_message(reporter.state), textAlign: TextAlign.center),
          ),
        ),
      ),
    );
  }

  String _message(CrewLocationReportingState state) => switch (state) {
        CrewLocationReportingState.reporting => 'Location reporting is active for this response.',
        CrewLocationReportingState.permissionDenied => 'Location permission is needed to report this ambulance position.',
        CrewLocationReportingState.permissionPermanentlyDenied => 'Enable location permission in device settings to report this ambulance position.',
        CrewLocationReportingState.unavailable => 'Location services are unavailable on this device.',
        CrewLocationReportingState.failed => 'Location reporting stopped. Reopen this screen to try again.',
        CrewLocationReportingState.stopped => 'There is no live response assigned to you.',
      };
}
