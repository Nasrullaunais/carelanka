import 'package:carelanka_mobile/features/emergency/widgets/run_card.dart';
import 'package:carelanka_mobile/services/api_client/models/call_priority.dart';
import 'package:carelanka_mobile/services/api_client/models/dispatch_detail.dart';
import 'package:carelanka_mobile/services/api_client/models/dispatch_status.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

Future<void> pumpCard(
  WidgetTester tester,
  DispatchDetail run, {
  bool busy = false,
  bool hasHandoverNotes = false,
  VoidCallback? onStep,
  DateTime Function()? now,
  VoidCallback? onEndAtScene,
  VoidCallback? onWriteHandoverNotes,
}) => tester.pumpWidget(
  MaterialApp(
    home: Scaffold(
      body: RunCard(
        run: run,
        busy: busy,
        onStep: onStep ?? () {},
        onDecline: () {},
        onNavigate: () {},
        onEndAtScene: onEndAtScene ?? () {},
        onWriteHandoverNotes: onWriteHandoverNotes ?? () {},
        hasHandoverNotes: hasHandoverNotes,
        now: now ?? DateTime.now,
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

  group('handover notes', () {
    for (final status in [
      DispatchStatus.atScene,
      DispatchStatus.transportingToHospital,
    ]) {
      testWidgets('can be written while ${status.name}', (tester) async {
        var opened = 0;
        await pumpCard(
          tester,
          DispatchDetail(status: status),
          onWriteHandoverNotes: () => opened++,
        );

        await tester.tap(find.text('Write handover notes'));

        expect(opened, 1);
      });
    }

    for (final status in [
      DispatchStatus.assigned,
      DispatchStatus.acknowledged,
      DispatchStatus.enRouteToScene,
    ]) {
      testWidgets('cannot be written before the scene: ${status.name}', (
        tester,
      ) async {
        await pumpCard(tester, DispatchDetail(status: status));

        expect(find.textContaining('handover notes'), findsNothing);
      });
    }

    testWidgets('say when something is already written', (tester) async {
      await pumpCard(
        tester,
        const DispatchDetail(status: DispatchStatus.atScene),
        hasHandoverNotes: true,
      );

      expect(find.text('Edit handover notes'), findsOneWidget);
      expect(find.text('Write handover notes'), findsNothing);
    });
  });

  group('confirming a step', () {
    DispatchDetail at(DispatchStatus status) => DispatchDetail(status: status);

    testWidgets('accepting and starting to drive take one tap', (tester) async {
      var taps = 0;
      await pumpCard(tester, at(DispatchStatus.assigned), onStep: () => taps++);
      await tester.tap(find.text('Accept this run'));
      expect(taps, 1);

      await pumpCard(
        tester,
        at(DispatchStatus.acknowledged),
        onStep: () => taps++,
      );
      await tester.tap(find.text('Start driving to the scene'));
      expect(taps, 2);
    });

    testWidgets(
      'handing over is confirmed by its own sheet, not a second tap',
      (tester) async {
        var taps = 0;
        await pumpCard(
          tester,
          at(DispatchStatus.transportingToHospital),
          onStep: () => taps++,
        );

        await tester.tap(find.text('Hand over at the hospital'));

        expect(taps, 1);
      },
    );

    for (final (status, label) in [
      (DispatchStatus.enRouteToScene, 'I have reached the scene'),
      (DispatchStatus.atScene, 'Patient on board, going to hospital'),
    ]) {
      testWidgets('"$label" needs a second tap', (tester) async {
        var taps = 0;
        await pumpCard(tester, at(status), onStep: () => taps++);

        await tester.tap(find.text(label));
        await tester.pump();

        expect(taps, 0);
        expect(find.text('Tap again to confirm'), findsOneWidget);
        expect(find.text(label), findsNothing);

        await tester.tap(find.text('Tap again to confirm'));
        await tester.pump();

        expect(taps, 1);
        expect(find.text(label), findsOneWidget);
      });
    }

    testWidgets('turns amber while it waits for the second tap', (
      tester,
    ) async {
      await pumpCard(tester, at(DispatchStatus.enRouteToScene));
      Color? background() => tester
          .widget<FilledButton>(find.byType(FilledButton))
          .style
          ?.backgroundColor
          ?.resolve({});
      expect(background(), isNull);

      await tester.tap(find.text('I have reached the scene'));
      await tester.pump();

      expect(background(), const Color(0xFFF5A524));
    });

    testWidgets('goes back to normal after 4 seconds', (tester) async {
      var taps = 0;
      await pumpCard(
        tester,
        at(DispatchStatus.enRouteToScene),
        onStep: () => taps++,
      );
      await tester.tap(find.text('I have reached the scene'));
      await tester.pump(const Duration(milliseconds: 3900));
      expect(find.text('Tap again to confirm'), findsOneWidget);

      await tester.pump(const Duration(milliseconds: 200));
      expect(find.text('I have reached the scene'), findsOneWidget);

      await tester.tap(find.text('I have reached the scene'));
      await tester.pump();
      expect(taps, 0);
    });

    testWidgets('goes back to normal when the status changes underneath', (
      tester,
    ) async {
      await pumpCard(tester, at(DispatchStatus.enRouteToScene));
      await tester.tap(find.text('I have reached the scene'));
      await tester.pump();

      await pumpCard(tester, at(DispatchStatus.atScene));

      expect(find.text('Tap again to confirm'), findsNothing);
      expect(find.text('Patient on board, going to hospital'), findsOneWidget);
    });

    testWidgets('going back to normal while busy stops a stray second tap', (
      tester,
    ) async {
      var taps = 0;
      final run = at(DispatchStatus.enRouteToScene);
      await pumpCard(tester, run, onStep: () => taps++);
      await tester.tap(find.text('I have reached the scene'));
      await tester.pump();

      await pumpCard(tester, run, busy: true, onStep: () => taps++);
      await pumpCard(tester, run, onStep: () => taps++);

      expect(find.text('I have reached the scene'), findsOneWidget);
      expect(taps, 0);
    });

    testWidgets('tells a screen reader to tap again', (tester) async {
      final semantics = tester.ensureSemantics();
      await pumpCard(tester, at(DispatchStatus.enRouteToScene));
      expect(find.bySemanticsLabel('I have reached the scene'), findsOneWidget);

      await tester.tap(find.text('I have reached the scene'));
      await tester.pump();

      expect(
        find.bySemanticsLabel('Tap again to confirm: I have reached the scene'),
        findsOneWidget,
      );
      semantics.dispose();
    });
  });

  group('the navigation button', () {
    for (final (status, label) in [
      (DispatchStatus.acknowledged, 'Navigate to scene'),
      (DispatchStatus.enRouteToScene, 'Navigate to scene'),
      (DispatchStatus.atScene, 'Navigate to scene'),
      (DispatchStatus.transportingToHospital, 'Navigate to hospital'),
    ]) {
      testWidgets('says "$label" while ${status.name}', (tester) async {
        await pumpCard(tester, DispatchDetail(status: status));

        expect(find.text(label), findsOneWidget);
      });
    }

    testWidgets('is not offered before the run is accepted', (tester) async {
      await pumpCard(
        tester,
        const DispatchDetail(status: DispatchStatus.assigned),
      );

      expect(find.textContaining('Navigate to'), findsNothing);
    });
  });

  group('the header', () {
    for (final (priority, label) in [
      (CallPriority.critical, 'Critical'),
      (CallPriority.high, 'High'),
      (CallPriority.medium, 'Medium'),
      (CallPriority.low, 'Low'),
    ]) {
      testWidgets('shows a "$label" chip', (tester) async {
        await pumpCard(tester, DispatchDetail(callPriority: priority));

        expect(find.text(label), findsOneWidget);
        expect(find.textContaining('Priority:'), findsNothing);
      });
    }

    testWidgets('tells a screen reader what the chip means', (tester) async {
      final semantics = tester.ensureSemantics();
      await pumpCard(
        tester,
        const DispatchDetail(callPriority: CallPriority.critical),
      );

      expect(find.bySemanticsLabel('Priority: Critical'), findsOneWidget);
      semantics.dispose();
    });

    testWidgets('shows no chip when the priority is missing', (tester) async {
      await pumpCard(tester, const DispatchDetail());

      expect(find.text('Critical'), findsNothing);
      expect(find.text('Unknown'), findsNothing);
    });

    testWidgets('only shows the ambulance, not empty or useless rows', (
      tester,
    ) async {
      await pumpCard(
        tester,
        const DispatchDetail(
          ambulanceRegistration: 'AMB-3',
          crewCount: 2,
          destinationWardName: 'Ward 4',
        ),
      );

      expect(find.text('AMB-3'), findsOneWidget);
      expect(find.text('Crew on board'), findsNothing);
      expect(find.text('Going to ward'), findsNothing);
      expect(find.text('Ward 4'), findsNothing);
    });
  });

  group('when the run was sent', () {
    final sentAt = DateTime.utc(2026, 9, 30, 8);

    testWidgets('says just now for the first minute', (tester) async {
      await pumpCard(
        tester,
        DispatchDetail(dispatchedAt: sentAt),
        now: () => sentAt.add(const Duration(seconds: 59)),
      );

      expect(find.text('Sent just now'), findsOneWidget);
    });

    testWidgets('counts the minutes', (tester) async {
      await pumpCard(
        tester,
        DispatchDetail(dispatchedAt: sentAt),
        now: () => sentAt.add(const Duration(minutes: 3, seconds: 20)),
      );

      expect(find.text('Sent 3 minutes ago'), findsOneWidget);
    });

    testWidgets('counts the hours once a long time has passed', (tester) async {
      await pumpCard(
        tester,
        DispatchDetail(dispatchedAt: sentAt),
        now: () => sentAt.add(const Duration(hours: 2, minutes: 5)),
      );

      expect(find.text('Sent 2 hours ago'), findsOneWidget);
    });

    testWidgets('moves on by itself when the next minute arrives', (
      tester,
    ) async {
      var now = sentAt.add(const Duration(seconds: 30));
      await pumpCard(
        tester,
        DispatchDetail(dispatchedAt: sentAt),
        now: () => now,
      );
      expect(find.text('Sent just now'), findsOneWidget);

      now = sentAt.add(const Duration(seconds: 61));
      await tester.pump(const Duration(seconds: 31));
      expect(find.text('Sent 1 minute ago'), findsOneWidget);

      now = sentAt.add(const Duration(seconds: 121));
      await tester.pump(const Duration(seconds: 60));
      expect(find.text('Sent 2 minutes ago'), findsOneWidget);
    });

    testWidgets('treats a phone clock running behind as just now', (
      tester,
    ) async {
      await pumpCard(
        tester,
        DispatchDetail(dispatchedAt: sentAt),
        now: () => sentAt.subtract(const Duration(seconds: 20)),
      );

      expect(find.text('Sent just now'), findsOneWidget);
    });

    testWidgets('shows nothing when the time is unknown', (tester) async {
      await pumpCard(tester, const DispatchDetail());

      expect(find.textContaining('Sent'), findsNothing);
    });
  });
}
