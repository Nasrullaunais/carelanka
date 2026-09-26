import 'package:carelanka_mobile/features/emergency/screens/report_emergency_screen.dart';
import 'package:carelanka_mobile/features/emergency/services/device_location.dart';
import 'package:carelanka_mobile/features/emergency/services/patient_emergency_service.dart';
import 'package:carelanka_mobile/features/emergency/state/caller_location_controller.dart';
import 'package:carelanka_mobile/features/emergency/state/patient_emergency_controller.dart';
import 'package:carelanka_mobile/services/api_client/models/create_emergency_call_request.dart';
import 'package:carelanka_mobile/services/api_client/models/emergency_call_detail.dart';
import 'package:carelanka_mobile/services/api_client/models/emergency_cancellation_request.dart';
import 'package:carelanka_mobile/services/api_client/models/my_call_tracking.dart';
import 'package:carelanka_mobile/services/api_client/models/my_emergency_call_summary.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';

import 'caller_location_controller_test.dart';

void main() {
  testWidgets(
    'opening the ambulance request screen does not fail during build',
    (tester) async {
      await tester.pumpWidget(
        MultiProvider(
          providers: [
            ChangeNotifierProvider(
              create: (_) => PatientEmergencyController(_Service()),
            ),
            ChangeNotifierProvider(
              create: (_) => CallerLocationController(FakeDeviceLocation()),
            ),
          ],
          child: const MaterialApp(home: ReportEmergencyScreen()),
        ),
      );
      expect(tester.takeException(), isNull);
      await tester.pump();
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets('the request button waits for a location', (tester) async {
    final location = FakeDeviceLocation();
    await tester.pumpWidget(
      MultiProvider(
        providers: [
          ChangeNotifierProvider(
            create: (_) => PatientEmergencyController(_Service()),
          ),
          ChangeNotifierProvider(
            create: (_) => CallerLocationController(location),
          ),
        ],
        child: const MaterialApp(home: ReportEmergencyScreen()),
      ),
    );
    await tester.pump();

    expect(find.text('Waiting for your location…'), findsOneWidget);

    location.emit(
      LocationFix(
        latitude: 6.9271,
        longitude: 79.8612,
        accuracyMetres: 12,
        capturedAt: DateTime.now().toUtc(),
      ),
    );
    await tester.pump();

    expect(find.text('Accurate to about 12 m.'), findsOneWidget);
    expect(find.text('Request ambulance'), findsOneWidget);
  });
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
