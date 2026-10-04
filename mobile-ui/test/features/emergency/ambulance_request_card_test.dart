import 'package:carelanka_mobile/features/emergency/services/patient_emergency_service.dart';
import 'package:carelanka_mobile/features/emergency/widgets/ambulance_request_card.dart';
import 'package:carelanka_mobile/services/api_client/models/call_status.dart';
import 'package:carelanka_mobile/services/api_client/models/my_emergency_call_summary.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';

import 'fake_patient_emergency_service.dart';

void main() {
  Future<void> pumpCard(
    WidgetTester tester,
    List<MyEmergencyCallSummary> calls,
  ) async {
    await tester.pumpWidget(
      Provider<PatientEmergencyService>.value(
        value: FakePatientEmergencyService(myCalls: calls),
        child: const MaterialApp(home: Scaffold(body: AmbulanceRequestCard())),
      ),
    );
    await tester.pump();
  }

  testWidgets('offers a new request when none is open', (tester) async {
    await pumpCard(tester, const [
      MyEmergencyCallSummary(id: 'old', status: CallStatus.completed),
    ]);

    expect(find.text('Request ambulance'), findsOneWidget);
    expect(find.text('Share your location'), findsOneWidget);
  });

  testWidgets('points back to the request that is still open', (tester) async {
    await pumpCard(tester, const [
      MyEmergencyCallSummary(id: 'live', status: CallStatus.enRoute),
    ]);

    expect(find.text('Ambulance on the way'), findsOneWidget);
    expect(find.text('Request ambulance'), findsNothing);
  });
}
