import 'package:carelanka_mobile/features/emergency/widgets/run_prompts.dart';
import 'package:carelanka_mobile/services/api_client/models/scene_outcome.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

Future<void> openSheet(
  WidgetTester tester, {
  void Function(SceneFinish?)? onClosed,
}) async {
  await tester.pumpWidget(
    MaterialApp(
      home: Builder(
        builder: (context) => Scaffold(
          body: TextButton(
            onPressed: () async {
              final finish = await askSceneOutcome(context);
              onClosed?.call(finish);
            },
            child: const Text('open'),
          ),
        ),
      ),
    ),
  );
  await tester.tap(find.text('open'));
  await tester.pumpAndSettle();
}

VoidCallback? finishAction(WidgetTester tester) => tester
    .widget<FilledButton>(find.widgetWithText(FilledButton, 'Finish run'))
    .onPressed;

void main() {
  group('the scene outcome sheet', () {
    testWidgets('lists every reason and cannot finish before one is picked', (
      tester,
    ) async {
      await openSheet(tester);

      expect(find.text('Why is nobody going to hospital?'), findsOneWidget);
      for (final reason in [
        'Treated at the scene',
        'Patient refused to go',
        'Patient not found at the location',
        'False alarm',
        'Patient died at the scene',
      ]) {
        expect(find.text(reason), findsOneWidget);
      }
      expect(finishAction(tester), isNull);
    });

    testWidgets('returns the picked reason with trimmed notes', (tester) async {
      SceneFinish? result;
      await openSheet(tester, onClosed: (finish) => result = finish);

      await tester.tap(find.text('Patient refused to go'));
      await tester.enterText(find.byType(TextField), '  Wants her own doctor ');
      await tester.pump();
      expect(finishAction(tester), isNotNull);
      await tester.tap(find.text('Finish run'));
      await tester.pumpAndSettle();

      expect(result?.outcome, SceneOutcome.patientRefused);
      expect(result?.notes, 'Wants her own doctor');
    });

    testWidgets('leaves the notes empty when nothing was written', (
      tester,
    ) async {
      SceneFinish? result;
      await openSheet(tester, onClosed: (finish) => result = finish);

      await tester.tap(find.text('False alarm'));
      await tester.enterText(find.byType(TextField), '   ');
      await tester.pump();
      await tester.tap(find.text('Finish run'));
      await tester.pumpAndSettle();

      expect(result?.outcome, SceneOutcome.falseAlarm);
      expect(result?.notes, isNull);
    });

    testWidgets('returns nothing when it is dismissed', (tester) async {
      var closed = false;
      SceneFinish? result = (outcome: SceneOutcome.falseAlarm, notes: null);
      await openSheet(
        tester,
        onClosed: (finish) {
          closed = true;
          result = finish;
        },
      );

      await tester.tapAt(const Offset(10, 10));
      await tester.pumpAndSettle();

      expect(closed, isTrue);
      expect(result, isNull);
    });
  });
}
