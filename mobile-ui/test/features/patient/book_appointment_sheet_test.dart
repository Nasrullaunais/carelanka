import 'package:carelanka_mobile/core/theme/app_theme.dart';
import 'package:carelanka_mobile/features/patient/screens/book_appointment_sheet.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:intl/intl.dart';

void main() {
  Future<Future<BookAppointmentRequestDraft?> Function()> pumpSheet(WidgetTester tester) async {
    tester.view.physicalSize = const Size(390, 1200);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    Future<BookAppointmentRequestDraft?>? result;
    await tester.pumpWidget(
      MaterialApp(
        theme: AppTheme.light,
        home: Builder(
          builder: (context) => Scaffold(
            body: Center(
              child: TextButton(
                onPressed: () => result = showBookAppointmentSheet(context),
                child: const Text('open'),
              ),
            ),
          ),
        ),
      ),
    );
    return () => result!;
  }

  Finder dayTile(DateTime day) => find.bySemanticsLabel(DateFormat('EEE, d MMM').format(day));

  testWidgets('a day from the strip and a suggested time make the booking', (tester) async {
    final result = await pumpSheet(tester);
    await tester.pumpAndSettle();
    await tester.tap(find.text('open'));
    await tester.pumpAndSettle();

    final inTwoDays = DateUtils.addDaysToDate(DateUtils.dateOnly(DateTime.now()), 2);
    await tester.tap(dayTile(inTwoDays));
    await tester.tap(find.text('10:00 AM'));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(FilledButton, 'Book'));
    await tester.pumpAndSettle();

    final draft = await result();
    expect(draft!.scheduledAt, DateTime(inTwoDays.year, inTwoDays.month, inTwoDays.day, 10));
  });

  testWidgets('Book stays off until both a day and a time are chosen', (tester) async {
    await pumpSheet(tester);
    await tester.pumpAndSettle();
    await tester.tap(find.text('open'));
    await tester.pumpAndSettle();

    FilledButton book() => tester.widget(find.widgetWithText(FilledButton, 'Book'));
    expect(book().onPressed, isNull);

    await tester.tap(find.text('9:00 AM'));
    await tester.pumpAndSettle();
    expect(book().onPressed, isNull);
  });
}
