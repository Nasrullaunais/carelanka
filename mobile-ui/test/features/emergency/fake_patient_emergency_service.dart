import 'package:carelanka_mobile/features/emergency/services/patient_emergency_service.dart';
import 'package:carelanka_mobile/services/api_client/models/create_emergency_call_request.dart';
import 'package:carelanka_mobile/services/api_client/models/emergency_call_detail.dart';
import 'package:carelanka_mobile/services/api_client/models/emergency_cancellation_request.dart';
import 'package:carelanka_mobile/services/api_client/models/my_call_tracking.dart';
import 'package:carelanka_mobile/services/api_client/models/my_emergency_call_summary.dart';

class FakePatientEmergencyService implements PatientEmergencyService {
  FakePatientEmergencyService({this.myCalls = const []});

  List<MyEmergencyCallSummary> myCalls;

  @override
  Future<List<MyEmergencyCallSummary>> calls() async => myCalls;

  @override
  Future<EmergencyCallDetail> report(CreateEmergencyCallRequest request) =>
      throw UnimplementedError();

  @override
  Future<MyCallTracking> track(String id) => throw UnimplementedError();

  @override
  Future<MyEmergencyCallSummary> cancel(String id, String reason) =>
      throw UnimplementedError();

  @override
  Future<EmergencyCancellationRequest> requestCancellation(
    String id,
    String reason,
  ) => throw UnimplementedError();
}
