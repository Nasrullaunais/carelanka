import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../services/api_client/care_lanka_api.dart';
import '../../services/api_client/models/principal_role.dart';
import 'screens/lab_reports_screen.dart';
import 'services/lab_reports_service.dart';
import 'services/report_file_source.dart';

class EquipmentPaths {
  const EquipmentPaths._();

  static const labReports = '/lab-reports';
}

final List<RouteBase> equipmentRoutes = [
  GoRoute(
    path: EquipmentPaths.labReports,
    builder: (context, _) => MultiProvider(
      providers: [
        Provider(create: (context) => LabReportsService(context.read<CareLankaApi>())),
        Provider<ReportFileSource>(create: (_) => DeviceReportFileSource()),
      ],
      child: const LabReportsScreen(),
    ),
  ),
];

// Uploading a lab report is limited to the equipment manager by the API.
String? equipmentHomePathFor(PrincipalRole role) => switch (role) {
      PrincipalRole.equipmentManager => EquipmentPaths.labReports,
      _ => null,
    };
