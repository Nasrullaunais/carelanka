import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../services/api_client/care_lanka_api.dart';
import '../../services/api_client/models/principal_role.dart';
import 'screens/my_stay_screen.dart';
import 'screens/pre_register_screen.dart';
import 'screens/nurse_worklist_screen.dart';
import 'services/patient_service.dart';
import 'state/my_stay_controller.dart';
import 'state/pre_register_controller.dart';
import 'state/worklist_controller.dart';

class PatientPaths {
  const PatientPaths._();

  static const worklist = '/ward/worklist';
  static const myStay = '/me/stay';
  static const preRegister = '/me/details';
}

/// Registered in `app.dart`. Controllers are created per route so a screen that
/// is popped does not keep polling or holding stale data.
final List<RouteBase> patientRoutes = [
  GoRoute(
    path: PatientPaths.worklist,
    builder: (context, _) => ChangeNotifierProvider(
      create: (context) => WorklistController(PatientService(context.read<CareLankaApi>())),
      child: const NurseWorklistScreen(),
    ),
  ),
  GoRoute(
    path: PatientPaths.myStay,
    builder: (context, _) => ChangeNotifierProvider(
      create: (context) => MyStayController(PatientService(context.read<CareLankaApi>())),
      child: const MyStayScreen(),
    ),
  ),
  GoRoute(
    path: PatientPaths.preRegister,
    builder: (context, _) => ChangeNotifierProvider(
      create: (context) => PreRegisterController(PatientService(context.read<CareLankaApi>())),
      child: const PreRegisterScreen(),
    ),
  ),
];

/// Where a role this component owns lands after signing in. Returns null for
/// roles that belong to another component.
String? patientHomePathFor(PrincipalRole role) => switch (role) {
      PrincipalRole.wardNurse => PatientPaths.worklist,
      PrincipalRole.patient => PatientPaths.myStay,
      _ => null,
    };
