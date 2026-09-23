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
    widget.reporter.resume();
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed) {
      widget.reporter.resume();
      return;
    }
    widget.reporter.pause();
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
