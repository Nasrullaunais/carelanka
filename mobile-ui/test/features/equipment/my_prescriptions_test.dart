import 'dart:typed_data';

import 'package:carelanka_mobile/core/network/api_exception.dart';
import 'package:carelanka_mobile/core/theme/app_theme.dart';
import 'package:carelanka_mobile/features/equipment/screens/my_prescriptions_screen.dart';
import 'package:carelanka_mobile/features/equipment/services/prescription_service.dart';
import 'package:carelanka_mobile/features/equipment/services/report_file_source.dart';
import 'package:carelanka_mobile/features/equipment/state/my_prescriptions_controller.dart';
import 'package:carelanka_mobile/services/api_client/models/prescription_status.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';

import 'fake_lab_reports.dart' show FakeReportFileSource;
import 'fake_prescriptions.dart';

PickedReport photo({int bytes = 2048}) =>
    PickedReport(bytes: Uint8List(bytes), name: 'prescription.jpg');

Widget app(FakePrescriptionService service, {FakeReportFileSource? files}) {
  return Provider<PrescriptionService>.value(
    value: service,
    child: MaterialApp(
      theme: AppTheme.light,
      home: MyPrescriptionsTab(files: files ?? FakeReportFileSource(photo: photo())),
    ),
  );
}

void main() {
  group('the prescriptions controller', () {
    test('will not send without a file', () async {
      final service = FakePrescriptionService();
      final controller = MyPrescriptionsController(service, FakeReportFileSource());

      expect(controller.validate(note: ''), 'Take a photo of the prescription or attach a PDF.');
      expect(await controller.upload(note: ''), isNull);
      expect(service.uploads, 0);
    });

    test('flags a file over 10 MB before sending', () async {
      final controller = MyPrescriptionsController(
        FakePrescriptionService(),
        FakeReportFileSource(photo: photo(bytes: 11 * 1024 * 1024)),
      );

      await controller.capturePhoto();

      expect(controller.validate(note: ''), 'The file is larger than 10 MB.');
    });

    test('a sent prescription goes to the top of the list with its note', () async {
      final service = FakePrescriptionService(
        prescriptions: [myPrescription(id: 'older', status: PrescriptionStatus.delivered, token: 3)],
      );
      final controller = MyPrescriptionsController(service, FakeReportFileSource(photo: photo()));

      await controller.load();
      await controller.capturePhoto();
      await controller.upload(note: '  After 4pm  ');

      expect(service.uploadedNote, 'After 4pm');
      expect(controller.attachment, isNull);
      expect(controller.prescriptions.valueOrNull!.map((p) => p.id), ['sent-1', 'older']);
    });

    test('keeps the photo when the upload is refused', () async {
      final service = FakePrescriptionService(
        uploadFailure: const ApiException(message: 'That file is empty.', statusCode: 400),
      );
      final controller = MyPrescriptionsController(service, FakeReportFileSource(photo: photo()));

      await controller.capturePhoto();
      final sent = await controller.upload(note: '');

      expect(sent, isNull);
      expect(controller.uploadFailure!.message, 'That file is empty.');
      expect(controller.attachment, isNotNull);
    });
  });

  group('the prescriptions tab', () {
    testWidgets('shows the token in large type once the pharmacy has it ready', (tester) async {
      await tester.pumpWidget(app(FakePrescriptionService(prescriptions: [
        myPrescription(status: PrescriptionStatus.ready, token: 7),
      ])));
      await tester.pumpAndSettle();

      expect(find.text('Ready to collect'), findsOneWidget);
      expect(find.text('Your token'), findsOneWidget);
      expect(find.text('7'), findsOneWidget);
    });

    testWidgets('shows delivered and the reason a prescription cannot be filled', (tester) async {
      await tester.pumpWidget(app(FakePrescriptionService(prescriptions: [
        myPrescription(id: 'a', status: PrescriptionStatus.delivered, token: 4),
        myPrescription(
          id: 'b',
          status: PrescriptionStatus.rejected,
          rejectionReason: 'The photo is blurred.',
        ),
      ])));
      await tester.pumpAndSettle();

      expect(find.text('Delivered'), findsOneWidget);
      expect(find.textContaining('Token 4'), findsOneWidget);
      expect(find.text("Can't be filled"), findsOneWidget);
      expect(find.text('The photo is blurred.'), findsOneWidget);
    });

    testWidgets('an unlinked account is told to add their details, with no upload button',
        (tester) async {
      await tester.pumpWidget(app(FakePrescriptionService(
        listFailure: const ApiException(message: 'not linked', statusCode: 404, code: 'cl_pat_033'),
      )));
      await tester.pumpAndSettle();

      expect(find.text('Complete your details'), findsOneWidget);
      expect(find.text('Upload prescription'), findsNothing);
    });

    testWidgets('uploading a photo adds it as waiting for the pharmacy', (tester) async {
      final service = FakePrescriptionService();
      await tester.pumpWidget(app(service));
      await tester.pumpAndSettle();

      await tester.tap(find.text('Upload prescription'));
      await tester.pumpAndSettle();

      await tester.tap(find.text('Photograph the prescription'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Send to the pharmacy'));
      await tester.pumpAndSettle();

      expect(service.uploads, 1);
      expect(find.text('Waiting for the pharmacy'), findsOneWidget);
      expect(find.textContaining('Prescription sent'), findsOneWidget);
    });

    testWidgets('fits a phone-width display', (tester) async {
      tester.view.physicalSize = const Size(360, 740);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(tester.view.reset);

      await tester.pumpWidget(app(FakePrescriptionService(prescriptions: [
        myPrescription(status: PrescriptionStatus.ready, token: 12, note: 'I will come after work.'),
      ])));
      await tester.pumpAndSettle();

      expect(tester.takeException(), isNull);
    });
  });
}
