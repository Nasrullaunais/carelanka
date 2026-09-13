import '../../../core/network/api.dart';
import '../../../services/api_client/care_lanka_api.dart';
import '../../../services/api_client/models/my_admission.dart';
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

  /// `cl_pat_033` — the signed-in account has no hospital record behind it yet.
  /// It comes back from every `/me/*` route, and means "show the details form",
  /// not "nothing found".
  static const notLinkedCode = 'cl_pat_033';

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

  Future<MyAdmission> loadMyAdmission() {
    return callApi(_api.patientSelfService.getMyAdmission);
  }

  Future<MyProfile> preRegister(PreRegisterRequest request) {
    return callApi(() => _api.patientSelfService.preRegisterSelf(body: request));
  }
}
