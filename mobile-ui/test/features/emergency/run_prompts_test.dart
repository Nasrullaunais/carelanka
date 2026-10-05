import 'package:carelanka_mobile/features/emergency/models/run_step.dart';
import 'package:carelanka_mobile/features/emergency/widgets/run_prompts.dart';
import 'package:carelanka_mobile/services/api_client/models/emergency_call_outcome.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

Future<void> _open(
  WidgetTester tester,
  Future<Object?> Function(BuildContext) ask,
  void Function(Object?) done,
) async {
  await tester.pumpWidget(
    MaterialApp(
      home: Scaffold(
        body: Builder(
          builder: (context) => TextButton(
            onPressed: () async => done(await ask(context)),
            child: const Text('Open'),
          ),
        ),
      ),
    ),
  );
  await tester.tap(find.text('Open'));
  await tester.pumpAndSettle();
}

FilledButton _button(WidgetTester tester, String label) =>
    tester.widget<FilledButton>(find.widgetWithText(FilledButton, label));

void main() {
  group('decline reason', () {
    testWidgets('Decline stays off until a real reason is typed', (
      tester,
    ) async {
      await _open(tester, askDeclineReason, (_) {});

      expect(_button(tester, 'Decline run').onPressed, isNull);
      await tester.enterText(find.byType(TextField), '    ');
      await tester.pump();
      expect(_button(tester, 'Decline run').onPressed, isNull);

      await tester.enterText(find.byType(TextField), 'Flat tyre');
      await tester.pump();
      expect(_button(tester, 'Decline run').onPressed, isNotNull);
    });

    testWidgets('the reason is sent without the spaces around it', (
      tester,
    ) async {
      Object? result = 'not closed';
      await _open(tester, askDeclineReason, (value) => result = value);

      await tester.enterText(find.byType(TextField), '   Vehicle problem  ');
      await tester.pump();
      await tester.tap(find.text('Decline run'));
      await tester.pumpAndSettle();

      expect(result, 'Vehicle problem');
    });

    testWidgets('a reason longer than the server limit of 500 is cut at 500', (
      tester,
    ) async {
      Object? result;
      await _open(tester, askDeclineReason, (value) => result = value);

      await tester.enterText(find.byType(TextField), 'x' * 501);
      await tester.pump();
      await tester.tap(find.text('Decline run'));
      await tester.pumpAndSettle();

      expect((result as String).length, 500);
    });

    testWidgets('Back declines nothing', (tester) async {
      Object? result = 'not closed';
      await _open(tester, askDeclineReason, (value) => result = value);

      await tester.enterText(find.byType(TextField), 'Flat tyre');
      await tester.tap(find.text('Back'));
      await tester.pumpAndSettle();

      expect(result, isNull);
    });
  });

  group('handover details', () {
    testWidgets('both fields are optional and blanks are sent as empty', (
      tester,
    ) async {
      Object? result;
      await _open(tester, askHandoverDetails, (value) => result = value);

      await tester.enterText(
        find.widgetWithText(
          TextField,
          'Patient condition on arrival (optional)',
        ),
        '   ',
      );
      await tester.tap(find.text('Confirm handover'));
      await tester.pumpAndSettle();

      expect(result, (notes: null, patientCondition: null));
    });

    testWidgets('typed details are trimmed and kept within the server limits', (
      tester,
    ) async {
      Object? result;
      await _open(tester, askHandoverDetails, (value) => result = value);

      await tester.enterText(
        find.widgetWithText(
          TextField,
          'Patient condition on arrival (optional)',
        ),
        ' Stable ',
      );
      await tester.enterText(
        find.widgetWithText(
          TextField,
          'Notes for the hospital team (optional)',
        ),
        'n' * 1001,
      );
      await tester.tap(find.text('Confirm handover'));
      await tester.pumpAndSettle();

      final details = result as HandoverDetails;
      expect(details.patientCondition, 'Stable');
      expect(details.notes!.length, 1000);
    });
  });

  group('ending a run at the scene', () {
    testWidgets('End the run stays off until an outcome is picked', (
      tester,
    ) async {
      Object? result;
      await _open(tester, askSceneOutcome, (value) => result = value);

      expect(_button(tester, 'End the run').onPressed, isNull);

      await tester.tap(find.text('Patient refused to come'));
      await tester.pump();
      await tester.tap(find.text('End the run'));
      await tester.pumpAndSettle();

      expect(result, (
        outcome: EmergencyCallOutcome.refusedTransport,
        notes: null,
      ));
    });

    testWidgets(
      'offers exactly the four outcomes the server accepts at a scene',
      (tester) async {
        await _open(tester, askSceneOutcome, (_) {});

        for (final outcome in sceneOutcomes) {
          expect(find.text(outcome.label), findsOneWidget);
        }
        expect(
          find.byType(RadioListTile<EmergencyCallOutcome>),
          findsNWidgets(4),
        );
        expect(find.text(EmergencyCallOutcome.transported.label), findsNothing);
        expect(find.text(EmergencyCallOutcome.falseAlarm.label), findsNothing);
      },
    );
  });
}
