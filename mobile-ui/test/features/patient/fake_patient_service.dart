import 'package:carelanka_mobile/core/network/api_exception.dart';
import 'package:carelanka_mobile/features/patient/services/patient_service.dart';
import 'package:carelanka_mobile/services/api_client/care_lanka_api.dart';
import 'package:carelanka_mobile/services/api_client/models/care_workflow_accepted.dart';
import 'package:carelanka_mobile/services/api_client/models/my_admission.dart';
import 'package:carelanka_mobile/services/api_client/models/my_appointment.dart';
import 'package:carelanka_mobile/services/api_client/models/my_appointment_paged_result.dart';
import 'package:carelanka_mobile/services/api_client/models/my_bill.dart';
import 'package:carelanka_mobile/services/api_client/models/my_care_recommendation_paged_result.dart';
import 'package:carelanka_mobile/services/api_client/models/my_profile.dart';
import 'package:carelanka_mobile/services/api_client/models/pre_register_request.dart';
import 'package:dio/dio.dart';

/// Answers with whatever each test hands it. The dio instance is never used —
/// every method here is overridden before it could reach the network.
class FakePatientService extends PatientService {
  FakePatientService() : super(CareLankaApi(Dio()));

  Object? admissionResult;
  Object? profileResult;
  Object? appointmentsResult;
  Object? bookResult;

  // Defaults to "no bill raised yet" - the ordinary state for a freshly admitted
  // patient, and what MyStayController._loadBill already treats as absent.
  Object? billResult =
      const ApiException(message: 'no bill', statusCode: 404, code: 'cl_pat_036');

  // Defaults to an empty page so a widget that reads its own history in initState
  // resolves immediately in a test that never sets this.
  Object? careRecommendationsResult = const MyCareRecommendationPagedResult(
    items: [],
    page: 1,
    pageSize: 20,
    totalItems: 0,
    totalPages: 1,
  );
  Object? submitCareQueryResult;
  String? lastCareQueryText;

  /// What the details form last sent, so a test can assert the form refused to
  /// submit at all rather than submitting something incomplete.
  PreRegisterRequest? savedDetails;
  Object? savedProfileResult;

  int bookCalls = 0;

  @override
  Future<MyAdmission> loadMyAdmission() async => _unwrap(admissionResult);

  @override
  Future<MyProfile> loadMyProfile() async => _unwrap(profileResult);

  @override
  Future<MyProfile> saveMyDetails(PreRegisterRequest request) async {
    savedDetails = request;
    return _unwrap(savedProfileResult ?? profileResult);
  }

  @override
  Future<MyAppointmentPagedResult> loadMyAppointments({int page = 1, int pageSize = 50}) async =>
      _unwrap(appointmentsResult);

  @override
  Future<MyBill> loadMyBill(String admissionId) async => _unwrap(billResult);

  @override
  Future<MyAppointment> bookAppointment({required DateTime scheduledAt, String? reason}) async {
    bookCalls++;
    return _unwrap(bookResult);
  }

  @override
  Future<MyCareRecommendationPagedResult> loadMyCareRecommendations({
    int page = 1,
    int pageSize = 20,
  }) async =>
      _unwrap(careRecommendationsResult);

  @override
  Future<CareWorkflowAccepted> submitCareQuery(String reportedText) async {
    lastCareQueryText = reportedText;
    return _unwrap(
      submitCareQueryResult ??
          const CareWorkflowAccepted(
            workflowId: 'w1',
            recommendationId: 'r1',
            status: 'running',
            pollUrl: '/api/care-workflows/w1',
          ),
    );
  }

  static T _unwrap<T>(Object? result) {
    if (result is ApiException) throw result;
    return result as T;
  }
}

MyAppointmentPagedResult appointmentPage(List<MyAppointment> items) => MyAppointmentPagedResult(
      items: items,
      page: 1,
      pageSize: 50,
      totalItems: items.length,
      totalPages: 1,
    );
