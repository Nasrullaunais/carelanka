import 'package:dio/dio.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../services/api_client/models/principal_role.dart';
import 'screens/leave_requests_screen.dart';
import 'screens/my_shifts_screen.dart';
import 'services/staff_leave_service.dart';
import 'services/staff_roster_service.dart';
import 'state/leave_requests_controller.dart';
import 'state/my_shifts_controller.dart';

class StaffPaths {
  const StaffPaths._();

  static const myShifts = '/staff/shifts';
  static const leave = '/staff/leave';
}

final List<RouteBase> staffRoutes = [
  GoRoute(
    path: StaffPaths.myShifts,
    builder: (context, _) => ChangeNotifierProvider(
      create: (context) => MyShiftsController(
        DioStaffRosterService(context.read<Dio>()),
      )..load(),
      child: const MyShiftsScreen(),
    ),
  ),
  GoRoute(
    path: StaffPaths.leave,
    builder: (context, _) => ChangeNotifierProvider(
      create: (context) => LeaveRequestsController(
        DioStaffLeaveService(context.read<Dio>()),
      )..load(),
      child: const LeaveRequestsScreen(),
    ),
  ),
];

String? staffHomePathFor(PrincipalRole role) => switch (role) {
      PrincipalRole.generalStaff ||
      PrincipalRole.wardNurse ||
      PrincipalRole.doctor =>
        StaffPaths.myShifts,
      _ => null,
    };
