import '../../../core/network/api.dart';
import '../../../services/api_client/care_lanka_api.dart';
import '../../../services/api_client/models/create_emergency_call_request.dart';
import '../../../services/api_client/models/emergency_call_detail.dart';
import '../../../services/api_client/models/emergency_cancellation_request.dart';
import '../../../services/api_client/models/my_call_tracking.dart';
import '../../../services/api_client/models/my_emergency_call_summary.dart';
import '../../../services/api_client/models/request_cancellation_request.dart';

abstract interface class PatientEmergencyService {
  Future<EmergencyCallDetail> report(CreateEmergencyCallRequest request);
  Future<List<MyEmergencyCallSummary>> calls();
  Future<MyCallTracking> track(String id);
  Future<MyEmergencyCallSummary> cancel(String id, String reason);
  Future<EmergencyCancellationRequest> requestCancellation(
    String id,
    String reason,
  );
}

final class GeneratedPatientEmergencyService
    implements PatientEmergencyService {
  GeneratedPatientEmergencyService(this._api);

  final CareLankaApi _api;

  @override
  Future<EmergencyCallDetail> report(CreateEmergencyCallRequest request) =>
      callApi(() => _api.calls.createEmergencyCall(body: request));

  @override
  Future<List<MyEmergencyCallSummary>> calls() async => (await callApi(
    () => _api.myCalls.getMyEmergencyCalls(pageSize: 20),
  )).items;

  @override
  Future<MyCallTracking> track(String id) =>
      callApi(() => _api.myCalls.trackMyEmergencyCall(id: id));

  @override
  Future<MyEmergencyCallSummary> cancel(String id, String reason) => callApi(
    () => _api.myCalls.cancelMyEmergencyCall(
      id: id,
      body: RequestCancellationRequest(reason: reason),
    ),
  );

  @override
  Future<EmergencyCancellationRequest> requestCancellation(
    String id,
    String reason,
  ) => callApi(
    () => _api.myCalls.requestMyEmergencyCallCancellation(
      id: id,
      body: RequestCancellationRequest(reason: reason),
    ),
  );
}
