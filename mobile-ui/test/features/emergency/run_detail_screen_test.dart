import 'package:carelanka_mobile/features/emergency/screens/run_detail_screen.dart';
import 'package:carelanka_mobile/features/emergency/state/run_detail_controller.dart';
import 'package:carelanka_mobile/services/api_client/models/dispatch_detail.dart';
import 'package:carelanka_mobile/services/api_client/models/dispatch_status.dart';
import 'package:carelanka_mobile/services/api_client/models/emergency_call_outcome.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';

import 'my_run_controller_test.dart';

void main() {
  Future<FakeRunService> pumpDetail(
    WidgetTester tester,
    DispatchDetail run,
  ) async {
    final service = FakeRunService()..finished = run;
    await tester.pumpWidget(
      ChangeNotifierProvider(
        create: (_) => RunDetailController(service, 'run-1'),
        child: const MaterialApp(home: RunDetailScreen()),
      ),
    );
    await tester.pump();
    return service;
  }

  testWidgets('a handed-over run shows how the patient was handed over', (
    tester,
  ) async {
    final service = await pumpDetail(
      tester,
      const DispatchDetail(
        status: DispatchStatus.handedOver,
        callOutcome: EmergencyCallOutcome.transported,
        sceneAddressLabel: 'Galle Face, Colombo',
        patientCondition: 'Conscious, stable',
        handoverNotes: 'Gave aspirin at 2:20 PM.',
      ),
    );

    expect(service.calls, contains('run:run-1'));
    expect(find.text('Handed over'), findsOneWidget);
    expect(find.text('Galle Face, Colombo'), findsOneWidget);
    expect(find.text('Taken to hospital'), findsOneWidget);
    expect(find.text('Conscious, stable'), findsOneWidget);
    expect(find.text('Gave aspirin at 2:20 PM.'), findsOneWidget);
  });

  testWidgets('a declined run shows the reason the crew gave', (tester) async {
    await pumpDetail(
      tester,
      const DispatchDetail(
        status: DispatchStatus.declined,
        declinedReason: 'Flat tyre',
      ),
    );

    expect(find.text('Declined'), findsOneWidget);
    expect(find.text('Reason given'), findsOneWidget);
    expect(find.text('Flat tyre'), findsOneWidget);
  });

  testWidgets('a run ended at the scene shows the note the crew wrote', (
    tester,
  ) async {
    await pumpDetail(
      tester,
      const DispatchDetail(
        status: DispatchStatus.closedAtScene,
        callOutcome: EmergencyCallOutcome.refusedTransport,
        callOutcomeNotes: 'Signed the refusal form',
      ),
    );

    expect(find.text('Patient refused to come'), findsOneWidget);
    expect(find.text('Signed the refusal form'), findsOneWidget);
  });

  testWidgets('a run given away does not claim how the call ended', (
    tester,
  ) async {
    await pumpDetail(
      tester,
      const DispatchDetail(
        status: DispatchStatus.reassigned,
        reassignmentReason: 'WP-CAL-102 is closer',
        callOutcome: EmergencyCallOutcome.falseAlarm,
        callOutcomeNotes: 'Caller said it was a prank',
      ),
    );

    expect(find.text('WP-CAL-102 is closer'), findsOneWidget);
    expect(find.text('False alarm'), findsNothing);
    expect(find.text('Caller said it was a prank'), findsNothing);
  });
}
