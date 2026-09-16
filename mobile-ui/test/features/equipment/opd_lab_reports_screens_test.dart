import 'dart:io';

import 'package:carelanka_mobile/core/network/api_exception.dart';
import 'package:carelanka_mobile/core/theme/app_theme.dart';
import 'package:carelanka_mobile/features/equipment/screens/lab_reports_screen.dart';
import 'package:carelanka_mobile/features/equipment/services/lab_reports_service.dart';
import 'package:carelanka_mobile/features/equipment/services/report_file_source.dart';
import 'package:carelanka_mobile/features/equipment/widgets/report_attachment.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';

import 'fake_lab_reports.dart';

Widget app(FakeLabReportsService service, FakeReportFileSource files) {
  return MultiProvider(
    providers: [
      Provider<LabReportsService>.value(value: service),
      Provider<ReportFileSource>.value(value: files),
    ],
    child: MaterialApp(theme: AppTheme.light, home: const LabReportsScreen()),
  );
}

Future<void> openPatient(WidgetTester tester) async {
  await tester.tap(find.text('OPD patient'));
  await tester.pumpAndSettle();
  await tester.enterText(find.byType(TextField), 'PB3M');
  await tester.pumpAndSettle();
  await tester.tap(find.text('Lab Outpatient Check'));
  await tester.pumpAndSettle();
}

void main() {
  late Directory temp;

  setUp(() => temp = Directory.systemTemp.createTempSync('opd_lab_screens'));
  tearDown(() => temp.deleteSync(recursive: true));

  testWidgets('the lab reports tab offers the OPD patient option', (tester) async {
    await tester.pumpWidget(app(FakeLabReportsService(), FakeReportFileSource()));

    expect(find.text('Lab reports'), findsOneWidget);
    expect(find.text('OPD patient'), findsOneWidget);
    expect(find.textContaining('discharged patient'), findsOneWidget);
  });

  testWidgets('searching finds a discharged patient and opens their reports', (tester) async {
    final service = FakeLabReportsService(
      patients: [opdPatient()],
      reports: [labReport(testName: 'Lipid profile')],
    );
    await tester.pumpWidget(app(service, FakeReportFileSource()));

    await openPatient(tester);

    expect(find.text('Lab Outpatient Check'), findsOneWidget);
    expect(find.textContaining('PB3MKWGJ'), findsOneWidget);
    expect(find.text('Lipid profile'), findsOneWidget);
    expect(find.text('Upload lab report'), findsOneWidget);
  });

  testWidgets('a search with no match says so', (tester) async {
    await tester.pumpWidget(app(FakeLabReportsService(patients: [opdPatient()]), FakeReportFileSource()));

    await tester.tap(find.text('OPD patient'));
    await tester.pumpAndSettle();
    await tester.enterText(find.byType(TextField), 'ZZZZ');
    await tester.pumpAndSettle();

    expect(find.text('No patient found'), findsOneWidget);
  });

  testWidgets('uploading a photographed report adds it to the patient', (tester) async {
    final service = FakeLabReportsService(patients: [opdPatient()]);
    final files = FakeReportFileSource(photo: reportFile(temp));
    await tester.pumpWidget(app(service, files));

    await openPatient(tester);
    expect(find.text('No lab reports yet'), findsOneWidget);

    await tester.tap(find.text('Upload lab report'));
    await tester.pumpAndSettle();
    await tester.enterText(find.widgetWithText(TextField, 'Test name'), 'Full blood count');
    await tester.tap(find.text('Photograph the report'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Upload report'));
    await tester.pumpAndSettle();

    expect(service.uploadedPatientId, 'patient-1');
    expect(find.text('Full blood count'), findsOneWidget);
    expect(find.text('Full blood count uploaded for Lab Outpatient Check.'), findsOneWidget);
  });

  testWidgets('the upload form explains what is missing and sends nothing', (tester) async {
    final service = FakeLabReportsService(patients: [opdPatient()]);
    await tester.pumpWidget(app(service, FakeReportFileSource()));

    await openPatient(tester);
    await tester.tap(find.text('Upload lab report'));
    await tester.pumpAndSettle();
    await tester.enterText(find.widgetWithText(TextField, 'Test name'), 'Full blood count');
    await tester.tap(find.text('Upload report'));
    await tester.pumpAndSettle();

    expect(find.text('Photograph the report or attach a PDF.'), findsOneWidget);
    expect(service.uploads, 0);
  });

  testWidgets('a file over 10 MB is flagged on the form', (tester) async {
    final files = FakeReportFileSource(
      photo: reportFile(temp, bytes: PickedReport.maxBytes + 1),
    );
    await tester.pumpWidget(app(FakeLabReportsService(patients: [opdPatient()]), files));

    await openPatient(tester);
    await tester.tap(find.text('Upload lab report'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Photograph the report'));
    await tester.pumpAndSettle();

    expect(find.textContaining('over the 10 MB limit'), findsOneWidget);
  });

  testWidgets('a rejected upload shows the server message and stays on the form',
      (tester) async {
    final service = FakeLabReportsService(
      patients: [opdPatient()],
      uploadFailure: const ApiException(message: 'No such patient.', statusCode: 404),
    );
    final files = FakeReportFileSource(photo: reportFile(temp));
    await tester.pumpWidget(app(service, files));

    await openPatient(tester);
    await tester.tap(find.text('Upload lab report'));
    await tester.pumpAndSettle();
    await tester.enterText(find.widgetWithText(TextField, 'Test name'), 'Full blood count');
    await tester.tap(find.text('Photograph the report'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Upload report'));
    await tester.pumpAndSettle();

    expect(find.text('No such patient.'), findsOneWidget);
    expect(find.text('Upload report'), findsOneWidget);
  });

  testWidgets('in a browser the upload buttons are off and say why', (tester) async {
    var photographed = false;

    await tester.pumpWidget(MaterialApp(
      home: Scaffold(
        body: ReportAttachment(
          attachment: null,
          enabled: true,
          uploadSupported: false,
          onPhotograph: () => photographed = true,
          onAttachPdf: () {},
          onRemove: () {},
        ),
      ),
    ));

    expect(find.textContaining('not in a browser'), findsOneWidget);
    await tester.tap(find.text('Photograph the report'));
    expect(photographed, isFalse);
  });

  testWidgets('every screen fits a phone-width display', (tester) async {
    tester.view.physicalSize = const Size(360, 740);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    await tester.pumpWidget(app(
      FakeLabReportsService(patients: [opdPatient()], reports: [labReport(summary: 'Normal.')]),
      FakeReportFileSource(),
    ));
    expect(tester.takeException(), isNull);

    await openPatient(tester);
    expect(tester.takeException(), isNull);

    await tester.tap(find.text('Upload lab report'));
    await tester.pumpAndSettle();
    expect(tester.takeException(), isNull);
  });
}
