import 'dart:io';

import '../../../core/network/api.dart';
import '../../../services/api_client/care_lanka_api.dart';
import '../../../services/api_client/models/lab_report.dart';
import '../../../services/api_client/models/lab_report_paged_result.dart';
import '../../../services/api_client/models/patient_summary_paged_result.dart';

class LabReportsService {
  const LabReportsService(this._api);

  final CareLankaApi _api;

  // Searches every registered patient, not only those on a ward, so a patient
  // who was discharged and came back to OPD for a blood test is still found.
  Future<PatientSummaryPagedResult> searchPatients(String search, {int pageSize = 20}) {
    return callApi(() => _api.patients.listPatients(search: search, pageSize: pageSize));
  }

  Future<LabReportPagedResult> loadReports(String patientId, {int page = 1, int pageSize = 50}) {
    return callApi(() => _api.laboratory.listLabReports(
          patientId: patientId,
          page: page,
          pageSize: pageSize,
        ));
  }

  Future<LabReport> uploadReport({
    required String patientId,
    required String testName,
    required File file,
    String? summary,
  }) {
    return callApi(() => _api.laboratory.uploadLabReport(
          patientId: patientId,
          testName: testName,
          file: file,
          summary: summary,
        ));
  }
}
