import 'package:dio/dio.dart';
import 'package:flutter/widgets.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../services/api_client/care_lanka_api.dart';
import '../../services/api_client/models/principal_role.dart';
import '../equipment/services/prescription_service.dart';
import 'screens/patient_shell.dart';
import 'services/patient_service.dart';
import 'state/appointments_controller.dart';
import 'state/my_stay_controller.dart';
import 'state/profile_controller.dart';

class PatientPaths {
  const PatientPaths._();

  static const home = '/me';
}

final List<RouteBase> patientRoutes = [
  GoRoute(path: PatientPaths.home, builder: (_, __) => const _PatientArea()),
];

/// Patient Management has no staff-facing screens on mobile — a ward nurse, reception, the
/// duty manager and the administrator all work through the web app. This app is the patient's.
String? patientHomePathFor(PrincipalRole role) => switch (role) {
      PrincipalRole.patient => PatientPaths.home,
      _ => null,
    };

class _PatientArea extends StatelessWidget {
  const _PatientArea();

  @override
  Widget build(BuildContext context) {
    final service = PatientService(context.read<CareLankaApi>());

    return MultiProvider(
      providers: [
        Provider<PatientService>.value(value: service),
        ChangeNotifierProvider(create: (_) => ProfileController(service)),
        ChangeNotifierProvider(create: (_) => MyStayController(service)),
        ChangeNotifierProvider(create: (_) => AppointmentsController(service)),
        Provider(
          create: (context) =>
              PrescriptionService(context.read<CareLankaApi>(), context.read<Dio>()),
        ),
      ],
      child: const PatientShell(),
    );
  }
}
