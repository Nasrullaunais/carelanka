import 'dart:async';
import 'dart:io';
import 'dart:typed_data';

import 'package:carelanka_mobile/core/network/api_exception.dart';
import 'package:carelanka_mobile/core/widgets/async_data.dart';
import 'package:carelanka_mobile/features/equipment/services/report_file_source.dart';
import 'package:carelanka_mobile/features/equipment/state/opd_patient_search_controller.dart';
import 'package:carelanka_mobile/features/equipment/state/patient_lab_reports_controller.dart';
import 'package:flutter_test/flutter_test.dart';

import 'fake_lab_reports.dart';

void main() {
  late Directory temp;

  setUp(() => temp = Directory.systemTemp.createTempSync('opd_lab_reports'));
  tearDown(() => temp.deleteSync(recursive: true));

  group('finding an OPD patient', () {
    test('does not search until two characters are typed', () async {
      final service = FakeLabReportsService(patients: [opdPatient()]);
      final controller = OpdPatientSearchController(service);

      await controller.setSearch('P');

      expect(service.searches, isEmpty);
      expect(controller.results, isNull);

      await controller.setSearch('PB');

      expect(service.searches, ['PB']);
      expect(controller.results!.valueOrNull!.single.patientCode, 'PB3MKWGJ');
    });

    test('a slow earlier search does not replace the latest results', () async {
      final service = FakeLabReportsService(patients: [
        opdPatient(id: 'a', code: 'PA000001', name: 'Amal Silva'),
        opdPatient(id: 'b', code: 'PB000002', name: 'Amali Perera'),
      ]);
      final slow = Completer<void>();
      service.heldSearches['Am'] = slow;
      final controller = OpdPatientSearchController(service);

      final first = controller.setSearch('Am');
      await controller.setSearch('Amali');
      slow.complete();
      await first;

      expect(controller.search, 'Amali');
      expect(controller.results!.valueOrNull!.map((p) => p.id), ['b']);
    });
  });

  group('uploading a report for the patient', () {
    PatientLabReportsController controllerFor(
      FakeLabReportsService service,
      FakeReportFileSource files,
    ) =>
        PatientLabReportsController(service, files, patient: opdPatient());

    test('loads the reports already filed for that patient', () async {
      final service = FakeLabReportsService(reports: [labReport()]);
      final controller = controllerFor(service, FakeReportFileSource());

      await controller.loadReports();

      expect(controller.reports.valueOrNull!.single.testName, 'Full blood count');
    });

    test('rejects the form before sending when something is missing or invalid', () async {
      final files = FakeReportFileSource();
      final controller = controllerFor(FakeLabReportsService(), files);

      expect(controller.validate(testName: 'X', summary: ''), 'Enter the test name.');
      expect(
        controller.validate(testName: 'Full blood count', summary: ''),
        'Photograph the report or attach a PDF.',
      );

      files.photo = reportFile(temp, name: 'report.docx');
      await controller.capturePhoto();
      expect(
        controller.validate(testName: 'Full blood count', summary: ''),
        'Only PDF, JPEG or PNG files can be uploaded.',
      );

      files.photo = reportFile(temp, bytes: PickedReport.maxBytes + 1);
      await controller.capturePhoto();
      expect(
        controller.validate(testName: 'Full blood count', summary: ''),
        'The file is larger than 10 MB.',
      );

      files.photo = reportFile(temp);
      await controller.capturePhoto();
      expect(controller.validate(testName: 'Full blood count', summary: ''), isNull);
    });

    test('uploads against the patient id and shows the new report first', () async {
      final service = FakeLabReportsService(reports: [labReport(id: 'older', testName: 'Lipid profile')]);
      final files = FakeReportFileSource(photo: reportFile(temp));
      final controller = controllerFor(service, files);

      await controller.loadReports();
      await controller.capturePhoto();
      final report = await controller.upload(
        testName: '  Full blood count  ',
        summary: '  Haemoglobin normal.  ',
      );

      expect(report, isNotNull);
      expect(service.uploadedPatientId, 'patient-1');
      expect(service.uploadedTestName, 'Full blood count');
      expect(service.uploadedSummary, 'Haemoglobin normal.');
      expect(controller.attachment, isNull);
      expect(controller.reports.valueOrNull!.map((r) => r.testName),
          ['Full blood count', 'Lipid profile']);
    });

    test('uploads a file picked in a browser, which has bytes but no path', () async {
      final service = FakeLabReportsService();
      final picked = PickedReport(bytes: Uint8List(2048), name: 'report.pdf');
      final controller = controllerFor(service, FakeReportFileSource(document: picked));

      await controller.pickDocument();

      expect(controller.attachment!.byteSize, 2048);
      expect(controller.validate(testName: 'Full blood count', summary: ''), isNull);

      await controller.upload(testName: 'Full blood count', summary: '');

      expect(service.uploads, 1);
      expect(service.uploadedReport, same(picked));
    });

    test('sends no summary when the summary is blank', () async {
      final service = FakeLabReportsService();
      final controller = controllerFor(service, FakeReportFileSource(photo: reportFile(temp)));

      await controller.capturePhoto();
      await controller.upload(testName: 'Full blood count', summary: '   ');

      expect(service.uploadedSummary, isNull);
    });

    test('keeps the attached file when the upload is rejected', () async {
      final service = FakeLabReportsService(
        uploadFailure: const ApiException(message: 'No such patient.', statusCode: 404),
      );
      final controller = controllerFor(service, FakeReportFileSource(photo: reportFile(temp)));

      await controller.capturePhoto();
      final report = await controller.upload(testName: 'Full blood count', summary: '');

      expect(report, isNull);
      expect(controller.uploadFailure!.message, 'No such patient.');
      expect(controller.attachment, isNotNull);
    });

    test('a second tap while uploading does not upload twice', () async {
      final service = FakeLabReportsService();
      final controller = controllerFor(service, FakeReportFileSource(photo: reportFile(temp)));

      await controller.capturePhoto();
      final first = controller.upload(testName: 'Full blood count', summary: '');
      final second = controller.upload(testName: 'Full blood count', summary: '');

      expect(await first, isNotNull);
      expect(await second, isNull);
      expect(service.uploads, 1);
    });

    test('cancelling the camera leaves nothing attached', () async {
      final controller = controllerFor(FakeLabReportsService(), FakeReportFileSource());

      await controller.capturePhoto();

      expect(controller.attachment, isNull);
      expect(controller.reports, isA<AsyncLoading>());
    });
  });
}
