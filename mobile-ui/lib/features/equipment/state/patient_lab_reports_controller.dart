import 'package:flutter/foundation.dart';

import '../../../core/network/api_exception.dart';
import '../../../core/widgets/async_data.dart';
import '../../../services/api_client/models/lab_report.dart';
import '../../../services/api_client/models/patient_summary.dart';
import '../services/lab_reports_service.dart';
import '../services/report_file_source.dart';

class PatientLabReportsController extends ChangeNotifier {
  PatientLabReportsController(this._service, this._files, {required this.patient});

  final LabReportsService _service;
  final ReportFileSource _files;
  final PatientSummary patient;

  AsyncData<List<LabReport>> _reports = const AsyncData.loading();
  PickedReport? _attachment;
  bool _uploading = false;
  ApiException? _uploadFailure;

  AsyncData<List<LabReport>> get reports => _reports;
  PickedReport? get attachment => _attachment;
  bool get uploading => _uploading;
  ApiException? get uploadFailure => _uploadFailure;

  Future<void> loadReports() async {
    _reports = const AsyncData.loading();
    notifyListeners();

    try {
      final page = await _service.loadReports(patient.id);
      _reports = AsyncData.ready(page.items);
    } on ApiException catch (error) {
      _reports = AsyncData.failed(error);
    }
    notifyListeners();
  }

  Future<void> capturePhoto() => _attach(_files.capturePhoto);

  Future<void> pickDocument() => _attach(_files.pickDocument);

  Future<void> _attach(Future<PickedReport?> Function() pick) async {
    final picked = await pick();
    if (picked == null) return;

    _attachment = picked;
    _uploadFailure = null;
    notifyListeners();
  }

  void removeAttachment() {
    _attachment = null;
    notifyListeners();
  }

  String? validate({required String testName, required String summary}) {
    final name = testName.trim();
    if (name.length < 2) return 'Enter the test name.';
    if (name.length > 120) return 'The test name must be 120 characters or fewer.';
    if (summary.trim().length > 1000) return 'The summary must be 1000 characters or fewer.';

    final file = _attachment;
    if (file == null) return 'Photograph the report or attach a PDF.';
    if (!file.hasAllowedType) return 'Only PDF, JPEG or PNG files can be uploaded.';
    if (file.isTooBig) return 'The file is larger than 10 MB.';

    return null;
  }

  Future<LabReport?> upload({required String testName, required String summary}) async {
    final file = _attachment;
    if (file == null || _uploading) return null;

    _uploading = true;
    _uploadFailure = null;
    notifyListeners();

    final writtenSummary = summary.trim();

    try {
      final report = await _service.uploadReport(
        patientId: patient.id,
        testName: testName.trim(),
        file: file.file,
        summary: writtenSummary.isEmpty ? null : writtenSummary,
      );

      _attachment = null;
      final current = _reports.valueOrNull ?? const <LabReport>[];
      _reports = AsyncData.ready([report, ...current]);
      return report;
    } on ApiException catch (error) {
      _uploadFailure = error;
      return null;
    } finally {
      _uploading = false;
      notifyListeners();
    }
  }
}
