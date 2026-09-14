import '../../../core/network/api.dart';
import '../../../services/api_client/care_lanka_api.dart';
import '../../../services/api_client/models/book_appointment_request.dart';
import '../../../services/api_client/models/my_admission.dart';
import '../../../services/api_client/models/my_admission_paged_result.dart';
import '../../../services/api_client/models/my_appointment.dart';
import '../../../services/api_client/models/my_appointment_paged_result.dart';
import '../../../services/api_client/models/my_profile.dart';
import '../../../services/api_client/models/pre_register_request.dart';
import '../../../services/api_client/models/worklist_row_paged_result.dart';

/// Every Patient Management call the mobile app makes.
///
/// Wraps the generated client rather than replacing it: the shapes stay
/// generated, and [callApi] is what turns a transport failure into an
/// `ApiException` the screens already know how to render.
class PatientService {
  const PatientService(this._api);

  final CareLankaApi _api;

  /// The signed-in account has no hospital record behind it yet. It arrives as
  /// a 404 from `/me/profile` and `/me/admission`, and as a 409 from booking a
  /// visit, so branch on this code rather than on the status.
  static const notLinkedCode = 'cl_pat_033';

  /// Linked, but not admitted right now. Also a 404, and not the same thing.
  static const noCurrentStayCode = 'cl_pat_034';

  Future<MyProfile> loadMyProfile() {
    return callApi(_api.patientSelfService.getMyProfile);
  }

  Future<MyProfile> saveMyDetails(PreRegisterRequest request) {
    return callApi(() => _api.patientSelfService.preRegisterSelf(body: request));
  }

  Future<MyAdmission> loadMyAdmission() {
    return callApi(_api.patientSelfService.getMyAdmission);
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
