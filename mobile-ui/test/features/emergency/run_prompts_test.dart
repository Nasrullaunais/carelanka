import 'package:carelanka_mobile/features/emergency/models/handover_draft.dart';
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

Future<void> openHandoverSheet(
  WidgetTester tester, {
  required HandoverSheetMode mode,
  HandoverDraft initial = HandoverDraft.empty,
  required ValueChanged<HandoverDraft> onChanged,
  ValueChanged<HandoverDraft?>? onClosed,
}) async {
  await tester.pumpWidget(
    MaterialApp(
      home: Builder(
        builder: (context) => Scaffold(
          body: TextButton(
            onPressed: () async {
              final result = await askHandoverDetails(
                context,
                mode: mode,
                initial: initial,
                onChanged: onChanged,
              );
              onClosed?.call(result);
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

  group('the handover sheet', () {
    testWidgets('opens with what was already written', (tester) async {
      await openHandoverSheet(
        tester,
        mode: HandoverSheetMode.handOver,
        initial: const HandoverDraft(
          patientCondition: 'Conscious',
          notes: 'Left leg splinted',
        ),
        onChanged: (_) {},
      );

      expect(find.text('Hand over the patient'), findsOneWidget);
      expect(find.text('Conscious'), findsOneWidget);
      expect(find.text('Left leg splinted'), findsOneWidget);
      expect(find.text('Confirm handover'), findsOneWidget);
    });

    testWidgets('keeps every edit even if the sheet is dismissed', (
      tester,
    ) async {
      final drafts = <HandoverDraft>[];
      HandoverDraft? closedWith = const HandoverDraft(notes: 'not closed yet');
      await openHandoverSheet(
        tester,
        mode: HandoverSheetMode.draft,
        onChanged: drafts.add,
        onClosed: (result) => closedWith = result,
      );

      await tester.enterText(
        find.widgetWithText(TextField, 'Patient condition (optional)'),
        'Breathing normally',
      );
      await tester.enterText(
        find.widgetWithText(
          TextField,
          'Notes for the hospital team (optional)',
        ),
        'Fall from a ladder',
      );
      await tester.tapAt(const Offset(10, 10));
      await tester.pumpAndSettle();

      expect(closedWith, isNull);
      expect(drafts.last.patientCondition, 'Breathing normally');
      expect(drafts.last.notes, 'Fall from a ladder');
    });

    testWidgets('saving notes closes the sheet with the draft', (tester) async {
      HandoverDraft? closedWith;
      await openHandoverSheet(
        tester,
        mode: HandoverSheetMode.draft,
        onChanged: (_) {},
        onClosed: (result) => closedWith = result,
      );

      expect(find.text('Handover notes'), findsOneWidget);
      await tester.enterText(
        find.widgetWithText(
          TextField,
          'Notes for the hospital team (optional)',
        ),
        '  Splinted  ',
      );
      await tester.tap(find.text('Save notes'));
      await tester.pumpAndSettle();

      expect(closedWith?.notesOrNull, 'Splinted');
      expect(closedWith?.patientConditionOrNull, isNull);
    });

    testWidgets('an empty handover can still be confirmed', (tester) async {
      HandoverDraft? closedWith;
      await openHandoverSheet(
        tester,
        mode: HandoverSheetMode.handOver,
        onChanged: (_) {},
        onClosed: (result) => closedWith = result,
      );

      await tester.tap(find.text('Confirm handover'));
      await tester.pumpAndSettle();

      expect(closedWith?.isEmpty, isTrue);
    });
  });
}
