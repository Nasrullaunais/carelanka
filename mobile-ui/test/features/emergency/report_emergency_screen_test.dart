import 'package:carelanka_mobile/features/emergency/screens/report_emergency_screen.dart';
import 'package:carelanka_mobile/features/emergency/services/patient_emergency_service.dart';
import 'package:carelanka_mobile/features/emergency/state/patient_emergency_controller.dart';
import 'package:carelanka_mobile/services/api_client/models/create_emergency_call_request.dart';
import 'package:carelanka_mobile/services/api_client/models/emergency_call_detail.dart';
import 'package:carelanka_mobile/services/api_client/models/emergency_cancellation_request.dart';
import 'package:carelanka_mobile/services/api_client/models/my_call_tracking.dart';
import 'package:carelanka_mobile/services/api_client/models/my_emergency_call_summary.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';

void main() {
  testWidgets(
    'opening the ambulance request screen does not fail during build',
    (tester) async {
      await tester.pumpWidget(
        ChangeNotifierProvider(
          create: (_) => PatientEmergencyController(_Service()),
          child: const MaterialApp(home: ReportEmergencyScreen()),
        ),
      );
      expect(tester.takeException(), isNull);
      await tester.pump();
      expect(tester.takeException(), isNull);
    },
  );
}

class _Service implements PatientEmergencyService {
  @override
  Future<List<MyEmergencyCallSummary>> calls() async => const [];

  @override
  Future<EmergencyCallDetail> report(CreateEmergencyCallRequest request) =>
      throw UnimplementedError();

  @override
  Future<MyCallTracking> track(String id) => throw UnimplementedError();

  @override
  Future<MyEmergencyCallSummary> cancel(String id, String reason) =>
      throw UnimplementedError();

  @override
  Future<EmergencyCancellationRequest> requestCancellation(
    String id,
    String reason,
  ) => throw UnimplementedError();
}
