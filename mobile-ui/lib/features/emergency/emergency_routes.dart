import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../services/api_client/care_lanka_api.dart';
import '../../services/api_client/models/principal_role.dart';
import 'screens/my_run_screen.dart';
import 'screens/report_emergency_screen.dart';
import 'screens/emergency_tracking_screen.dart';
import 'screens/run_history_screen.dart';
import 'services/crew_location_reporter.dart';
import 'services/crew_run_service.dart';
import 'state/my_run_controller.dart';
import 'state/run_history_controller.dart';
import 'services/patient_emergency_service.dart';
import 'state/patient_emergency_controller.dart';

class EmergencyPaths {
  const EmergencyPaths._();

  static const myRun = '/crew/run';
  static const history = '/crew/run/history';
  static const patientReport = '/me/emergency';
  static const patientTracking = '/me/emergency/track';
}

final List<RouteBase> emergencyRoutes = [
  GoRoute(
    path: EmergencyPaths.patientReport,
    builder: (context, _) => ChangeNotifierProvider(
      create: (context) => PatientEmergencyController(
        GeneratedPatientEmergencyService(context.read<CareLankaApi>()),
      ),
      child: const ReportEmergencyScreen(),
    ),
  ),
  GoRoute(
    path: '${EmergencyPaths.patientTracking}/:id',
    builder: (context, state) => ChangeNotifierProvider(
      create: (context) => PatientEmergencyController(
        GeneratedPatientEmergencyService(context.read<CareLankaApi>()),
      ),
      child: EmergencyTrackingScreen(callId: state.pathParameters['id']!),
    ),
  ),
  GoRoute(
    path: EmergencyPaths.history,
    builder: (context, _) => ChangeNotifierProvider(
      create: (context) => RunHistoryController(
        GeneratedCrewRunService(context.read<CareLankaApi>()),
      ),
      child: const RunHistoryScreen(),
    ),
  ),
  GoRoute(
    path: EmergencyPaths.myRun,
    builder: (context, _) => MultiProvider(
      providers: [
        ChangeNotifierProvider(
          create: (context) => CrewLocationReporter(
            dispatches: GeneratedCrewDispatchGateway(
              context.read<CareLankaApi>(),
            ),
            location: GeolocatorCrewLocationGateway(),
          ),
        ),
        ChangeNotifierProvider(
          create: (context) => MyRunController(
            GeneratedCrewRunService(context.read<CareLankaApi>()),
          ),
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
