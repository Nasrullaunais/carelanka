import 'package:flutter/widgets.dart';

import '../services/crew_location_reporter.dart';

final class CrewLocationLifecycle extends StatefulWidget {
  const CrewLocationLifecycle({
    super.key,
    required this.reporter,
    required this.child,
  });

  final CrewLocationReporter reporter;
  final Widget child;

  @override
  State<CrewLocationLifecycle> createState() => _CrewLocationLifecycleState();
}

final class _CrewLocationLifecycleState extends State<CrewLocationLifecycle>
    with WidgetsBindingObserver {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) widget.reporter.resume();
    });
  }

  // Reporting carries on in the background, because the crew drives with
  // Google Maps open. Coming back re-checks access the crew may have changed.
  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    switch (state) {
      case AppLifecycleState.resumed:
        if (widget.reporter.state != CrewLocationReportingState.reporting) {
          widget.reporter.resume();
        }
      case AppLifecycleState.detached:
        widget.reporter.stop();
      case AppLifecycleState.inactive ||
          AppLifecycleState.hidden ||
          AppLifecycleState.paused:
        break;
    }
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    widget.reporter.stop();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => widget.child;
}
