import 'package:dio/dio.dart';

import '../../../core/network/api.dart';
import '../../../services/api_client/care_lanka_api.dart';
import '../../../services/api_client/models/lab_report.dart';
import '../../../services/api_client/models/lab_report_paged_result.dart';
import '../../../services/api_client/models/patient_summary_paged_result.dart';
import 'report_file_source.dart';

class LabReportsService {
  const LabReportsService(this._api, this._dio);

  final CareLankaApi _api;
  final Dio _dio;

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
    required PickedReport report,
    String? summary,
  }) {
    final bytes = report.bytes;
    if (bytes == null) {
      return callApi(() => _api.laboratory.uploadLabReport(
            patientId: patientId,
            testName: testName,
            file: report.file!,
            summary: summary,
          ));
    }

    // Mirrors the generated uploadLabReport, which only accepts a dart:io File.
    return callApi(() async {
      final response = await _dio.post<Map<String, Object?>>(
        '/lab-reports',
        data: FormData.fromMap({
          'PatientId': patientId,
          'TestName': testName,
          'File': MultipartFile.fromBytes(bytes, filename: report.name),
          if (summary != null) 'Summary': summary,
        }),
      );
      return LabReport.fromJson(response.data!);
    });
  }
}
