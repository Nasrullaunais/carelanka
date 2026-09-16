import 'package:dio/dio.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../services/api_client/care_lanka_api.dart';
import '../../services/api_client/models/principal_role.dart';
import 'screens/equipment_home_screen.dart';
import 'screens/lab_reports_screen.dart';
import 'services/equipment_confirmation_service.dart';
import 'services/lab_reports_service.dart';
import 'services/maintenance_confirmation_service.dart';
import 'services/report_file_source.dart';
import 'state/equipment_confirmation_controller.dart';
import 'state/maintenance_confirmation_controller.dart';

class EquipmentPaths {
  const EquipmentPaths._();

  static const labReports = '/lab-reports';

  static const confirmation = '/equipment-confirmation';
}

final List<RouteBase> equipmentRoutes = [
  GoRoute(
    path: EquipmentPaths.labReports,
    builder: (context, _) => MultiProvider(
      providers: [
        Provider(create: (context) => LabReportsService(
              context.read<CareLankaApi>(),
              context.read<Dio>(),
            )),
        Provider<ReportFileSource>(create: (_) => DeviceReportFileSource()),
      ],
      child: const LabReportsScreen(),
    ),
  ),
  GoRoute(
    path: EquipmentPaths.confirmation,
    builder: (context, _) => MultiProvider(
      providers: [
        ChangeNotifierProvider(
          create: (context) => EquipmentConfirmationController(
            EquipmentConfirmationService(context.read<CareLankaApi>()),
          ),
        ),
        ChangeNotifierProvider(
          create: (context) => MaintenanceConfirmationController(
            MaintenanceConfirmationService(context.read<CareLankaApi>()),
          ),
        ),
      ],
      child: const EquipmentHomeScreen(),
    ),
  ),
];

// Uploading a lab report is limited to the equipment manager by the API, and confirming a newly
// registered item to the hospital administrator, so nobody approves their own entry.
String? equipmentHomePathFor(PrincipalRole role) => switch (role) {
      PrincipalRole.equipmentManager => EquipmentPaths.labReports,
      PrincipalRole.hospitalAdministrator => EquipmentPaths.confirmation,
      _ => null,
    };
