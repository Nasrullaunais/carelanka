import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../services/api_client/care_lanka_api.dart';
import '../../services/api_client/models/principal_role.dart';
import 'screens/my_run_screen.dart';
import 'services/crew_location_reporter.dart';
import 'services/crew_run_service.dart';
import 'state/my_run_controller.dart';

class EmergencyPaths {
  const EmergencyPaths._();

  static const myRun = '/crew/run';
}

final List<RouteBase> emergencyRoutes = [
  GoRoute(
    path: EmergencyPaths.myRun,
    builder: (context, _) => MultiProvider(
      providers: [
        ChangeNotifierProvider(
          create: (context) => CrewLocationReporter(
            dispatches: GeneratedCrewDispatchGateway(context.read<CareLankaApi>()),
            location: GeolocatorCrewLocationGateway(),
          ),
        ),
        ChangeNotifierProvider(
          create: (context) => MyRunController(GeneratedCrewRunService(context.read<CareLankaApi>())),
        ),
      ],
      child: const MyRunScreen(),
    ),
  ),
];

String? emergencyHomePathFor(PrincipalRole role) => switch (role) {
      PrincipalRole.ambulanceCrew => EmergencyPaths.myRun,
      _ => null,
    };
