import 'package:carelanka_mobile/features/emergency/screens/report_emergency_screen.dart';
import 'package:carelanka_mobile/features/emergency/services/device_location.dart';
import 'package:carelanka_mobile/features/emergency/state/caller_location_controller.dart';
import 'package:carelanka_mobile/features/emergency/state/patient_emergency_controller.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';

import 'caller_location_controller_test.dart';
import 'fake_patient_emergency_service.dart';

void main() {
  testWidgets(
    'opening the ambulance request screen does not fail during build',
    (tester) async {
      await tester.pumpWidget(
        MultiProvider(
          providers: [
            ChangeNotifierProvider(
              create: (_) =>
                  PatientEmergencyController(FakePatientEmergencyService()),
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
            create: (_) =>
                PatientEmergencyController(FakePatientEmergencyService()),
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

  testWidgets('a caller who cannot share a location is offered the 1990 line', (
    tester,
  ) async {
    final location = FakeDeviceLocation()
      ..requested = LocationAccess.deniedForever;
    await tester.pumpWidget(
      MultiProvider(
        providers: [
          ChangeNotifierProvider(
            create: (_) =>
                PatientEmergencyController(FakePatientEmergencyService()),
          ),
          ChangeNotifierProvider(
            create: (_) => CallerLocationController(location),
          ),
        ],
        child: const MaterialApp(home: ReportEmergencyScreen()),
      ),
    );
    await tester.pump();

    expect(find.text('Location is blocked for CareLanka'), findsOneWidget);
    expect(find.text('Call 1990'), findsOneWidget);
  });
}
