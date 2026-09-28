import 'package:provider/single_child_widget.dart';
import 'package:carelanka_mobile/core/auth/auth_controller.dart';
import 'package:carelanka_mobile/core/auth/patient_login_screen.dart';
import 'package:carelanka_mobile/core/auth/patient_register_screen.dart';
import 'package:carelanka_mobile/core/auth/session_expiry.dart';
import 'package:carelanka_mobile/core/auth/staff_login_screen.dart';
import 'package:carelanka_mobile/core/auth/token_store.dart';
import 'package:carelanka_mobile/core/auth/welcome_screen.dart';
import 'package:carelanka_mobile/core/theme/app_theme.dart';
import 'package:carelanka_mobile/features/emergency/screens/report_emergency_screen.dart';
import 'package:carelanka_mobile/features/emergency/services/patient_emergency_service.dart';
import 'package:carelanka_mobile/features/emergency/state/caller_location_controller.dart';
import 'package:carelanka_mobile/features/emergency/state/patient_emergency_controller.dart';
import 'package:carelanka_mobile/features/equipment/screens/equipment_confirmation_screen.dart';
import 'package:carelanka_mobile/features/equipment/screens/equipment_home_screen.dart';
import 'package:carelanka_mobile/features/equipment/screens/lab_reports_screen.dart';
import 'package:carelanka_mobile/features/equipment/screens/maintenance_confirmation_screen.dart';
import 'package:carelanka_mobile/features/equipment/screens/my_prescriptions_screen.dart';
import 'package:carelanka_mobile/features/equipment/services/lab_reports_service.dart';
import 'package:carelanka_mobile/features/equipment/services/prescription_service.dart';
import 'package:carelanka_mobile/features/equipment/services/report_file_source.dart';
import 'package:carelanka_mobile/features/equipment/state/equipment_confirmation_controller.dart';
import 'package:carelanka_mobile/features/equipment/state/maintenance_confirmation_controller.dart';
import 'package:carelanka_mobile/features/staff/models/my_shift_item.dart';
import 'package:carelanka_mobile/features/staff/models/staff_leave_item.dart';
import 'package:carelanka_mobile/features/staff/screens/leave_requests_screen.dart';
import 'package:carelanka_mobile/features/staff/screens/my_shifts_screen.dart';
import 'package:carelanka_mobile/features/staff/services/staff_leave_service.dart';
import 'package:carelanka_mobile/features/staff/services/staff_roster_service.dart';
import 'package:carelanka_mobile/features/staff/state/leave_requests_controller.dart';
import 'package:carelanka_mobile/features/staff/state/my_shifts_controller.dart';
import 'package:carelanka_mobile/services/api_client/care_lanka_api.dart';
import 'package:carelanka_mobile/services/api_client/models/create_emergency_call_request.dart';
import 'package:carelanka_mobile/services/api_client/models/emergency_call_detail.dart';
import 'package:carelanka_mobile/services/api_client/models/emergency_cancellation_request.dart';
import 'package:carelanka_mobile/services/api_client/models/my_call_tracking.dart';
import 'package:carelanka_mobile/services/api_client/models/my_emergency_call_summary.dart';
import 'package:carelanka_mobile/services/api_client/models/prescription_status.dart';
import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';

import '../test/features/emergency/caller_location_controller_test.dart' show FakeDeviceLocation;
import '../test/features/equipment/fake_confirmations.dart';
import '../test/features/equipment/fake_lab_reports.dart';
import '../test/features/equipment/fake_prescriptions.dart';
import 'patient_shots_test.dart' show loadRealFonts, phone;

class _NoEmergencies implements PatientEmergencyService {
  @override
  Future<List<MyEmergencyCallSummary>> calls() async => const [];
  @override
  Future<EmergencyCallDetail> report(CreateEmergencyCallRequest request) =>
      throw UnimplementedError();
  @override
  Future<MyCallTracking> track(String id) => throw UnimplementedError();
  @override
  Future<MyEmergencyCallSummary> cancel(String id, String reason) => throw UnimplementedError();
  @override
  Future<EmergencyCancellationRequest> requestCancellation(String id, String reason) =>
      throw UnimplementedError();
}

class _Roster implements StaffRosterService {
  @override
  Future<List<MyShiftItem>> getMyShifts({DateTime? from, DateTime? to}) async {
    String day(int offset) =>
        DateTime.now().add(Duration(days: offset)).toIso8601String().split('T').first;
    return [
      MyShiftItem(
        allocationId: 'a1',
        shiftId: 's1',
        wardName: 'Ward A · General',
        date: day(0),
        startTime: '07:00',
        endTime: '15:00',
        crossesMidnight: false,
        status: 'confirmed',
        clockedInAt: DateTime.now().subtract(const Duration(hours: 2)),
        canClockIn: false,
        wasReassigned: false,
      ),
      MyShiftItem(
        allocationId: 'a2',
        shiftId: 's2',
        wardName: 'ICU',
        date: day(1),
        startTime: '19:00',
        endTime: '07:00',
        crossesMidnight: true,
        status: 'confirmed',
        canClockIn: false,
        wasReassigned: true,
      ),
    ];
  }

  @override
  Future<void> clockIn(String allocationId) async {}
  @override
  Future<void> clockOut(String allocationId) async {}
}

class _Leave implements StaffLeaveService {
  @override
  Future<List<StaffLeaveItem>> getMyLeaveRequests({String? status}) async => const [
    StaffLeaveItem(
      id: 'l1',
      type: 'annual',
      isUrgent: false,
      startDate: '2026-10-12',
      endDate: '2026-10-14',
      reason: 'Family wedding in Kandy',
      status: 'pending',
      affectedShiftsCount: 2,
    ),
    StaffLeaveItem(
      id: 'l2',
      type: 'sick',
      isUrgent: true,
      startDate: '2026-09-02',
      endDate: '2026-09-02',
      status: 'approved',
    ),
    StaffLeaveItem(
      id: 'l3',
      type: 'annual',
      isUrgent: false,
      startDate: '2026-08-20',
      endDate: '2026-08-21',
      status: 'rejected',
      reviewNotes: 'Ward is short that week.',
    ),
  ];

  @override
  Future<StaffLeaveItem> createLeaveRequest({
    required String type,
    required String startDate,
    required String endDate,
    String? reason,
    String? swapShiftId,
    String? swapWithStaffMemberId,
  }) => throw UnimplementedError();

  @override
  Future<void> withdrawLeaveRequest(String id) async {}
}

void main() {
  setUpAll(loadRealFonts);

  late AuthController auth;

  // A fixed second rather than pumpAndSettle: some screens run a spinner that never stops.
  Future<void> settle(WidgetTester tester) async {
    for (var i = 0; i < 10; i++) {
      await tester.pump(const Duration(milliseconds: 100));
    }
  }

  Future<void> pump(WidgetTester tester, ThemeData theme, Widget home, List<SingleChildWidget> extra) async {
    tester.view.physicalSize = phone * 2;
    tester.view.devicePixelRatio = 2;
    addTearDown(tester.view.reset);

    final sessionExpiry = SessionExpiry();
    addTearDown(sessionExpiry.dispose);
    auth = AuthController(api: CareLankaApi(Dio()), tokens: TokenStore(), sessionExpiry: sessionExpiry);
    addTearDown(auth.dispose);

    await tester.pumpWidget(
      MultiProvider(
        providers: [ChangeNotifierProvider.value(value: auth), ...extra],
        child: MaterialApp(debugShowCheckedModeBanner: false, theme: theme, home: home),
      ),
    );
    await settle(tester);
  }

  Future<void> shot(WidgetTester tester, String name) async {
    await settle(tester);
    await expectLater(find.byType(MaterialApp), matchesGoldenFile('shots/$name.png'));
  }

  Future<void> unlock(WidgetTester tester) async {
    await tester.enterText(find.byType(TextField), confirmationCode);
    await tester.tap(find.text('Unlock'));
    await settle(tester);
  }

  for (final (mode, theme) in [('light', AppTheme.light), ('dark', AppTheme.dark)]) {
    testWidgets('sign in screens, $mode', (tester) async {
      await pump(tester, theme, const WelcomeScreen(), []);
      await shot(tester, 'auth_welcome_$mode');
      await pump(tester, theme, const PatientLoginScreen(), []);
      await shot(tester, 'auth_patient_login_$mode');
      await pump(tester, theme, const PatientRegisterScreen(), []);
      await shot(tester, 'auth_register_$mode');
      await pump(tester, theme, const StaffLoginScreen(), []);
      await shot(tester, 'auth_staff_login_$mode');
    });

    testWidgets('emergency request, $mode', (tester) async {
      await pump(tester, theme, const ReportEmergencyScreen(), [
        ChangeNotifierProvider(create: (_) => PatientEmergencyController(_NoEmergencies())),
        ChangeNotifierProvider(create: (_) => CallerLocationController(FakeDeviceLocation())),
      ]);
      await shot(tester, 'emergency_report_$mode');
    });

    testWidgets('equipment screens, $mode', (tester) async {
      await pump(tester, theme, const EquipmentHomeScreen(), [
        ChangeNotifierProvider(
          create: (_) => EquipmentConfirmationController(
            FakeEquipmentConfirmationService(items: [pendingItem()]),
          ),
        ),
        ChangeNotifierProvider(
          create: (_) => MaintenanceConfirmationController(
            FakeMaintenanceConfirmationService(jobs: [openJob(), openJob(id: 'job-2')]),
          ),
        ),
      ]);
      await shot(tester, 'equipment_home_$mode');

      await pump(tester, theme, const EquipmentConfirmationScreen(), [
        ChangeNotifierProvider(
          create: (_) => EquipmentConfirmationController(
            FakeEquipmentConfirmationService(items: [pendingItem()]),
          ),
        ),
      ]);
      await shot(tester, 'equipment_confirm_locked_$mode');
      await unlock(tester);
      await shot(tester, 'equipment_confirm_$mode');

      await pump(tester, theme, const MaintenanceConfirmationScreen(), [
        ChangeNotifierProvider(
          create: (_) => MaintenanceConfirmationController(
            FakeMaintenanceConfirmationService(jobs: [openJob()]),
          ),
        ),
      ]);
      await unlock(tester);
      await shot(tester, 'equipment_maintenance_$mode');

      final labs = FakeLabReportsService(
        patients: [opdPatient()],
        reports: [labReport(testName: 'Lipid profile'), labReport(testName: 'Full blood count')],
      );
      await pump(tester, theme, const LabReportsScreen(), [
        Provider<LabReportsService>.value(value: labs),
        Provider<ReportFileSource>.value(value: FakeReportFileSource()),
      ]);
      await shot(tester, 'equipment_lab_reports_$mode');
      await tester.tap(find.text('OPD patient'));
      await settle(tester);
      await tester.enterText(find.byType(TextField), 'PB3M');
      await settle(tester);
      await shot(tester, 'equipment_lab_search_$mode');
      await tester.tap(find.text('Lab Outpatient Check'));
      await shot(tester, 'equipment_lab_patient_$mode');

      await pump(tester, theme, MyPrescriptionsTab(files: FakeReportFileSource()), [
        Provider<PrescriptionService>.value(
          value: FakePrescriptionService(
            prescriptions: [
              myPrescription(id: 'r', status: PrescriptionStatus.ready, token: 7),
              myPrescription(id: 'd', status: PrescriptionStatus.delivered, token: 4),
              myPrescription(
                id: 'x',
                status: PrescriptionStatus.rejected,
                rejectionReason: 'The photo is blurred.',
              ),
            ],
          ),
        ),
      ]);
      await shot(tester, 'equipment_prescriptions_list_$mode');
    });

    testWidgets('staff screens, $mode', (tester) async {
      await pump(tester, theme, const MyShiftsScreen(), [
        ChangeNotifierProvider(create: (_) => MyShiftsController(_Roster())..load()),
      ]);
      await shot(tester, 'staff_shifts_$mode');
      await pump(tester, theme, const LeaveRequestsScreen(), [
        ChangeNotifierProvider(create: (_) => LeaveRequestsController(_Leave())..load()),
      ]);
      await shot(tester, 'staff_leave_$mode');
    });
  }
}
