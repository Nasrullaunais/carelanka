import 'dart:async';
import 'dart:io';

import 'package:carelanka_mobile/core/network/api_exception.dart';
import 'package:carelanka_mobile/features/equipment/services/lab_reports_service.dart';
import 'package:carelanka_mobile/features/equipment/services/report_file_source.dart';
import 'package:carelanka_mobile/services/api_client/models/gender.dart';
import 'package:carelanka_mobile/services/api_client/models/lab_report.dart';
import 'package:carelanka_mobile/services/api_client/models/lab_report_paged_result.dart';
import 'package:carelanka_mobile/services/api_client/models/patient_summary.dart';
import 'package:carelanka_mobile/services/api_client/models/patient_summary_paged_result.dart';

class FakeLabReportsService implements LabReportsService {
  FakeLabReportsService({
    this.patients = const [],
    this.reports = const [],
    this.uploadFailure,
  });

  List<PatientSummary> patients;
  List<LabReport> reports;
  ApiException? uploadFailure;

  final searches = <String>[];
  final Map<String, Completer<void>> heldSearches = {};

  String? uploadedPatientId;
  String? uploadedTestName;
  String? uploadedSummary;
  PickedReport? uploadedReport;
  int uploads = 0;

  @override
  Future<PatientSummaryPagedResult> searchPatients(String search, {int pageSize = 20}) async {
    searches.add(search);

    final hold = heldSearches[search];
    if (hold != null) await hold.future;

    final matches = patients
        .where((p) =>
            p.fullName.toLowerCase().contains(search.toLowerCase()) ||
            p.patientCode.toLowerCase().contains(search.toLowerCase()))
        .toList();

    return PatientSummaryPagedResult(
      items: matches,
      page: 1,
      pageSize: pageSize,
      totalItems: matches.length,
      totalPages: 1,
    );
  }

  @override
  Future<LabReportPagedResult> loadReports(String patientId, {int page = 1, int pageSize = 50}) async {
    return LabReportPagedResult(
      items: reports,
      page: page,
      pageSize: pageSize,
      totalItems: reports.length,
      totalPages: 1,
    );
  }

  @override
  Future<LabReport> uploadReport({
    required String patientId,
    required String testName,
    required PickedReport report,
    String? summary,
  }) async {
    uploads++;
    uploadedPatientId = patientId;
    uploadedTestName = testName;
    uploadedSummary = summary;
    uploadedReport = report;

    final failure = uploadFailure;
    if (failure != null) throw failure;

    return labReport(patientId: patientId, testName: testName, summary: summary);
  }
}

class FakeReportFileSource implements ReportFileSource {
  FakeReportFileSource({this.photo, this.document});

  PickedReport? photo;
  PickedReport? document;

  @override
  Future<PickedReport?> capturePhoto() async => photo;

  @override
  Future<PickedReport?> pickDocument() async => document;
}

PatientSummary opdPatient({
  String id = 'patient-1',
  String code = 'PB3MKWGJ',
  String name = 'Lab Outpatient Check',
  String? nic = '199012345678',
}) =>
    PatientSummary(id: id, patientCode: code, fullName: name, nic: nic, gender: Gender.male);

LabReport labReport({
  String id = 'report-1',
  String patientId = 'patient-1',
  String testName = 'Full blood count',
  String? summary,
}) =>
    LabReport(
      id: id,
      patientId: patientId,
      testName: testName,
      summary: summary,
      fileName: 'report.jpg',
      contentType: 'image/jpeg',
      byteSize: 1024,
      uploadedByStaffId: 'staff-1',
      createdAt: DateTime.utc(2026, 9, 16, 4, 30),
    );

PickedReport reportFile(Directory dir, {String name = 'report.jpg', int bytes = 1024}) {
  final file = File('${dir.path}/$name')..writeAsBytesSync(List.filled(bytes, 0));
  return PickedReport(file: file, name: name);
}
