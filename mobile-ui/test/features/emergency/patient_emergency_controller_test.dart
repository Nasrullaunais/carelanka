import 'package:carelanka_mobile/core/network/api_exception.dart';
import 'package:carelanka_mobile/features/emergency/services/patient_emergency_service.dart';
import 'package:carelanka_mobile/features/emergency/state/patient_emergency_controller.dart';
import 'package:carelanka_mobile/services/api_client/models/call_status.dart';
import 'package:carelanka_mobile/services/api_client/models/create_emergency_call_request.dart';
import 'package:carelanka_mobile/services/api_client/models/emergency_call_detail.dart';
import 'package:carelanka_mobile/services/api_client/models/emergency_cancellation_request.dart';
import 'package:carelanka_mobile/services/api_client/models/my_call_tracking.dart';
import 'package:carelanka_mobile/services/api_client/models/my_emergency_call_summary.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('a failed submission retry keeps the same idempotency key', () async {
    final service = _FakeService()..reportFailures = 1;
    final controller = PatientEmergencyController(service);

    expect(await _report(controller), isNull);
    expect(await _report(controller), 'call-1');

    expect(service.requests, hasLength(2));
    expect(
      service.requests.first.idempotencyKey,
      service.requests.last.idempotencyKey,
    );
    controller.dispose();
  });

  test('a successful submission starts a fresh idempotency key', () async {
    final service = _FakeService();
    final controller = PatientEmergencyController(service);

    expect(await _report(controller), 'call-1');
    expect(await _report(controller), 'call-1');

    expect(
      service.requests.first.idempotencyKey,
      isNot(service.requests.last.idempotencyKey),
    );
    controller.dispose();
  });

  test(
    'cancellation uses direct and reviewed operations at the right boundary',
    () async {
      final service = _FakeService();
      final controller = PatientEmergencyController(service);

      expect(
        await controller.cancel(
          'call-1',
          'Created by mistake',
          dispatched: false,
        ),
        isTrue,
      );
      expect(
        await controller.cancel('call-1', 'No longer needed', dispatched: true),
        isTrue,
      );

      expect(service.directCancellations, 1);
      expect(service.reviewedCancellations, 1);
      controller.dispose();
    },
  );
}

Future<String?> _report(PatientEmergencyController controller) =>
    controller.report(
      patientIsCaller: true,
      latitude: 6.9271,
      longitude: 79.8612,
      accuracy: 8,
      capturedAt: DateTime.utc(2026, 9, 23),
      details: 'Chest pain',
    );

class _FakeService implements PatientEmergencyService {
  int reportFailures = 0;
  int directCancellations = 0;
  int reviewedCancellations = 0;
  final requests = <CreateEmergencyCallRequest>[];

  @override
  Future<EmergencyCallDetail> report(CreateEmergencyCallRequest request) async {
    requests.add(request);
    if (reportFailures-- > 0) {
      throw const ApiException(message: 'offline');
    }
    return const EmergencyCallDetail(id: 'call-1');
  }

  @override
  Future<List<MyEmergencyCallSummary>> calls() async => const [];

  @override
  Future<MyCallTracking> track(String id) async => const MyCallTracking(
    emergencyCallId: 'call-1',
    callStatus: CallStatus.received,
  );

  @override
  Future<MyEmergencyCallSummary> cancel(String id, String reason) async {
    directCancellations++;
    return const MyEmergencyCallSummary(id: 'call-1');
  }

  @override
  Future<EmergencyCancellationRequest> requestCancellation(
    String id,
    String reason,
  ) async {
    reviewedCancellations++;
    return const EmergencyCancellationRequest(emergencyCallId: 'call-1');
  }
}
