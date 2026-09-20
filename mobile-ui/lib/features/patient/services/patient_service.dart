import '../../../core/network/api.dart';
import '../../../services/api_client/care_lanka_api.dart';
import '../../../services/api_client/models/book_appointment_request.dart';
import '../../../services/api_client/models/claim_by_patient_code_request.dart';
import '../../../services/api_client/models/my_admission.dart';
import '../../../services/api_client/models/my_admission_paged_result.dart';
import '../../../services/api_client/models/my_appointment.dart';
import '../../../services/api_client/models/my_appointment_paged_result.dart';
import '../../../services/api_client/models/my_bill.dart';
import '../../../services/api_client/models/my_lab_report_paged_result.dart';
import '../../../services/api_client/models/my_profile.dart';
import '../../../services/api_client/models/patient_claim_preview.dart';
import '../../../services/api_client/models/patient_medical_profile.dart';
import '../../../services/api_client/models/update_medical_profile_request.dart';
import '../../../services/api_client/models/pre_register_request.dart';
import '../../../services/api_client/models/worklist_row_paged_result.dart';

class PatientService {
  const PatientService(this._api);

  final CareLankaApi _api;

  // Arrives as 404 from /me/profile and /me/admission, and 409 from booking — branch on this code, not the status.
  static const notLinkedCode = 'cl_pat_033';

  // Also a 404, but distinct from notLinkedCode: linked, just not admitted right now.
  static const noCurrentStayCode = 'cl_pat_034';

  // A bill row only exists once the billing desk raises one, so a patient admitted this
  // morning usually has none. Ordinary state, not a failure.
  static const noBillCode = 'cl_pat_036';

  // One code for every way a claim fails, so the reply never confirms a code is real.
  static const claimNotMatchedCode = 'cl_pat_037';

  // This login already owns a record, so it cannot take a second one.
  static const alreadyLinkedCode = 'cl_pat_004';

  Future<MyProfile> loadMyProfile() {
    return callApi(_api.patientSelfService.getMyProfile);
  }

  Future<MyProfile> saveMyDetails(PreRegisterRequest request) {
    return callApi(() => _api.patientSelfService.preRegisterSelf(body: request));
  }

  Future<MyAdmission> loadMyAdmission() {
    return callApi(_api.patientSelfService.getMyAdmission);
  }

  Future<MyBill> loadMyBill(String admissionId) {
    return callApi(() => _api.patientSelfService.getMyBill(admissionId: admissionId));
  }

  // Also a 404: the visit ended in an admission, so its bill is there instead.
  static const appointmentBilledOnItsAdmissionCode = 'cl_pat_035';

  Future<MyBill> loadMyAppointmentBill(String appointmentId) {
    return callApi(
        () => _api.patientSelfService.getMyAppointmentBill(appointmentId: appointmentId));
  }

  Future<PatientClaimPreview> previewClaim({
    required String patientCode,
    required String nic,
  }) {
    return callApi(() => _api.patientSelfService.previewMyClaim(
          body: _claimRequest(patientCode, nic),
        ));
  }

  Future<MyProfile> claimRecord({
    required String patientCode,
    required String nic,
  }) {
    return callApi(() => _api.patientSelfService.claimMyRecord(
          body: _claimRequest(patientCode, nic),
        ));
  }

  static ClaimByPatientCodeRequest _claimRequest(String patientCode, String nic) {
    return ClaimByPatientCodeRequest(
      patientCode: patientCode.trim().toUpperCase(),
      nic: nic.trim(),
    );
  }

  Future<MyAdmissionPagedResult> loadMyHistory({int page = 1, int pageSize = 20}) {
    return callApi(() => _api.patientSelfService.getMyHistory(page: page, pageSize: pageSize));
  }

  Future<MyAppointmentPagedResult> loadMyAppointments({int page = 1, int pageSize = 50}) {
    return callApi(
        () => _api.patientSelfService.listMyAppointments(page: page, pageSize: pageSize));
  }

  Future<MyAppointment> bookAppointment({required DateTime scheduledAt, String? reason}) {
    return callApi(() => _api.patientSelfService.bookMyAppointment(
          body: BookAppointmentRequest(scheduledAt: scheduledAt.toUtc(), reason: reason),
        ));
  }

  Future<MyAppointment> cancelAppointment(String appointmentId) {
    return callApi(() => _api.patientSelfService.cancelMyAppointment(id: appointmentId));
  }

  Future<MyLabReportPagedResult> loadMyLabReports({int page = 1, int pageSize = 20}) {
    return callApi(
        () => _api.patientSelfService.getMyLabReports(page: page, pageSize: pageSize));
  }

  /// The relative path for [downloadBytes] — the generated client's own
  /// `downloadMyLabReport` corrupts binary content, see [downloadBytes].
  static String labReportFilePath(String reportId) => '/me/lab-reports/$reportId/file';

  /// The four free-text fields the care advisory agent reads. Returns a profile with every
  /// field null when nobody has written one — an empty profile is ordinary, not a 404.
  Future<PatientMedicalProfile> loadMedicalProfile(String patientId) {
    return callApi(() => _api.patients.getPatientMedicalProfile(id: patientId));
  }

  /// A full replace: whatever is left out of [request] is cleared on the record.
  Future<PatientMedicalProfile> saveMedicalProfile(
    String patientId,
    UpdateMedicalProfileRequest request,
  ) {
    return callApi(
        () => _api.patients.replacePatientMedicalProfile(id: patientId, body: request));
  }

  Future<WorklistRowPagedResult> loadWorklist({
    String? search,
    bool includeFinished = false,
    int page = 1,
    int pageSize = 20,
  }) {
    return callApi(() => _api.admissions.listPatientWorklist(
          search: (search == null || search.isEmpty) ? null : search,
          includeFinished: includeFinished,
          page: page,
          pageSize: pageSize,
        ));
  }
}
