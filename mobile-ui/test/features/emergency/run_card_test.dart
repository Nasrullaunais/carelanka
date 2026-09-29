import 'package:carelanka_mobile/features/emergency/widgets/run_card.dart';
import 'package:carelanka_mobile/services/api_client/models/dispatch_detail.dart';
import 'package:carelanka_mobile/services/api_client/models/dispatch_status.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

Future<void> pumpCard(
  WidgetTester tester,
  DispatchDetail run, {
  bool busy = false,
  VoidCallback? onEndAtScene,
}) => tester.pumpWidget(
  MaterialApp(
    home: Scaffold(
      body: RunCard(
        run: run,
        busy: busy,
        onStep: () {},
        onDecline: () {},
        onNavigate: () {},
        onEndAtScene: onEndAtScene ?? () {},
      ),
    ),
  ),
);

void main() {
  group('scene', () {
    testWidgets('shows the reverse-geocoded scene address when available', (
      tester,
    ) async {
      await pumpCard(
        tester,
        const DispatchDetail(sceneAddressLabel: 'Galle Face, Colombo'),
      );

      expect(find.text('Scene'), findsOneWidget);
      expect(find.text('Galle Face, Colombo'), findsOneWidget);
    });

    testWidgets('falls back to coordinates while the street name is missing', (
      tester,
    ) async {
      await pumpCard(
        tester,
        const DispatchDetail(sceneLatitude: 6.9271, sceneLongitude: 79.8612),
      );

      expect(find.text('6.92710, 79.86120'), findsOneWidget);
      expect(find.text('Street name not available yet'), findsOneWidget);
    });

    testWidgets('shows what the caller said, and hides the line when empty', (
      tester,
    ) async {
      await pumpCard(
        tester,
        const DispatchDetail(sceneDetails: 'Father collapsed'),
      );
      expect(find.text('“Father collapsed”'), findsOneWidget);

      await pumpCard(tester, const DispatchDetail(sceneDetails: '  '));
      expect(find.textContaining('“'), findsNothing);
    });

    testWidgets('warns when the location is not precise', (tester) async {
      await pumpCard(
        tester,
        const DispatchDetail(
          sceneLocationAccuracyMetres: 300,
          callerPhone: '0771234567',
        ),
      );

      expect(find.textContaining('approximate (±300 m)'), findsOneWidget);
      expect(find.textContaining('Call the caller'), findsOneWidget);
    });

    testWidgets('does not warn about a precise location', (tester) async {
      await pumpCard(
        tester,
        const DispatchDetail(sceneLocationAccuracyMetres: 50),
      );

      expect(find.textContaining('approximate'), findsNothing);
    });

    testWidgets('suggests checking on arrival when there is no phone', (
      tester,
    ) async {
      await pumpCard(
        tester,
        const DispatchDetail(sceneLocationAccuracyMetres: 120),
      );

      expect(find.textContaining('when you arrive'), findsOneWidget);
    });
  });

  group('caller', () {
    testWidgets('offers a call button and says who the caller is', (
      tester,
    ) async {
      await pumpCard(
        tester,
        const DispatchDetail(
          callerName: 'Nimal Perera',
          callerPhone: '0771234567',
          patientIsCaller: false,
        ),
      );

      expect(
        find.text('Caller: Nimal Perera · for someone else'),
        findsOneWidget,
      );
      expect(find.text('0771234567'), findsOneWidget);
      expect(find.text('Call caller'), findsOneWidget);
    });

    testWidgets('says when the caller is the patient', (tester) async {
      await pumpCard(
        tester,
        const DispatchDetail(callerName: 'Nimal', patientIsCaller: true),
      );

      expect(find.text('Caller: Nimal · the patient'), findsOneWidget);
    });

    testWidgets('hides the call button when there is no number', (
      tester,
    ) async {
      await pumpCard(tester, const DispatchDetail(callerName: 'Nimal Perera'));

      expect(find.text('Call caller'), findsNothing);
      expect(find.text('No phone number recorded'), findsOneWidget);
    });

    testWidgets('says so when nothing is known about the caller', (
      tester,
    ) async {
      await pumpCard(tester, const DispatchDetail());

      expect(find.text('No caller details recorded'), findsOneWidget);
      expect(find.text('Call caller'), findsNothing);
    });
  });

  group('finishing at the scene', () {
    const finish = 'Finish without going to hospital';

    testWidgets('is offered at the scene, under the hospital step', (
      tester,
    ) async {
      var finished = 0;
      await pumpCard(
        tester,
        const DispatchDetail(status: DispatchStatus.atScene),
        onEndAtScene: () => finished++,
      );

      await tester.tap(find.text(finish));

      expect(finished, 1);
      expect(find.text('Patient on board, going to hospital'), findsOneWidget);
    });

    for (final status in [
      DispatchStatus.assigned,
      DispatchStatus.acknowledged,
      DispatchStatus.enRouteToScene,
      DispatchStatus.transportingToHospital,
    ]) {
      testWidgets('is not offered while ${status.name}', (tester) async {
        await pumpCard(tester, DispatchDetail(status: status));

        expect(find.text(finish), findsNothing);
      });
    }

    testWidgets('cannot be tapped while another action is running', (
      tester,
    ) async {
      var finished = 0;
      await pumpCard(
        tester,
        const DispatchDetail(status: DispatchStatus.atScene),
        busy: true,
        onEndAtScene: () => finished++,
      );

      await tester.tap(find.text(finish));

      expect(finished, 0);
    });
  });
}
