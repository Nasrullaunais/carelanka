import 'package:flutter/widgets.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../services/api_client/care_lanka_api.dart';
import '../../services/api_client/models/principal_role.dart';
import 'screens/nurse_worklist_screen.dart';
import 'screens/patient_shell.dart';
import 'services/patient_service.dart';
import 'state/appointments_controller.dart';
import 'state/my_stay_controller.dart';
import 'state/profile_controller.dart';
import 'state/worklist_controller.dart';

class PatientPaths {
  const PatientPaths._();

  static const home = '/me';
  static const worklist = '/ward/worklist';
}

/// Registered in `app.dart`.
final List<RouteBase> patientRoutes = [
  GoRoute(path: PatientPaths.home, builder: (_, __) => const _PatientArea()),
  GoRoute(
    path: PatientPaths.worklist,
    builder: (context, _) => ChangeNotifierProvider(
      create: (context) => WorklistController(PatientService(context.read<CareLankaApi>())),
      child: const NurseWorklistScreen(),
    ),
  ),
];

/// Where a role this component owns lands after signing in. Returns null for
/// roles that belong to another component.
String? patientHomePathFor(PrincipalRole role) => switch (role) {
      PrincipalRole.patient => PatientPaths.home,
      PrincipalRole.wardNurse => PatientPaths.worklist,
      _ => null,
    };

/// The tabs share these, so they are created once here rather than per tab —
/// switching tabs should not throw away a loaded list.
class _PatientArea extends StatelessWidget {
  const _PatientArea();

  @override
  Widget build(BuildContext context) {
    final service = PatientService(context.read<CareLankaApi>());

    return MultiProvider(
      providers: [
        ChangeNotifierProvider(create: (_) => ProfileController(service)),
        ChangeNotifierProvider(create: (_) => MyStayController(service)),
        ChangeNotifierProvider(create: (_) => AppointmentsController(service)),
      ],
      child: const PatientShell(),
    );
  }
}
