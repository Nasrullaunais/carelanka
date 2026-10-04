import 'package:carelanka_mobile/core/network/api_exception.dart';
import 'package:carelanka_mobile/features/emergency/services/patient_emergency_service.dart';
import 'package:carelanka_mobile/features/emergency/state/patient_emergency_controller.dart';
import 'package:carelanka_mobile/services/api_client/models/call_status.dart';
import 'package:carelanka_mobile/services/api_client/models/cancellation_request_status.dart';
import 'package:carelanka_mobile/services/api_client/models/dispatch_status.dart';
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
    'before any ambulance is sent, cancelling happens straight away',
    () async {
      final service = _FakeService();
      final controller = PatientEmergencyController(service);
      await controller.refreshTracking('call-1');

      expect(controller.cancelNeedsReview, isFalse);
      expect(await controller.cancel('call-1', 'Created by mistake'), isTrue);

      expect(service.directCancellations, 1);
      expect(service.reviewedCancellations, 0);
      controller.dispose();
    },
  );

  test(
    'with an ambulance on the way, cancelling asks the duty manager',
    () async {
      final service = _FakeService()
        ..tracking = const MyCallTracking(
          callStatus: CallStatus.dispatched,
          dispatchStatus: DispatchStatus.enRouteToScene,
        );
      final controller = PatientEmergencyController(service);
      await controller.refreshTracking('call-1');

      expect(controller.cancelNeedsReview, isTrue);
      expect(await controller.cancel('call-1', 'Found a taxi'), isTrue);

      expect(service.reviewedCancellations, 1);
      expect(service.directCancellations, 0);
      controller.dispose();
    },
  );

  test('after a crew declines, the caller can still cancel directly', () async {
    final service = _FakeService()
      ..tracking = const MyCallTracking(
        callStatus: CallStatus.received,
        lookingForAnotherAmbulance: true,
      );
    final controller = PatientEmergencyController(service);
    await controller.refreshTracking('call-1');

    expect(controller.canCancel, isTrue);
    expect(await controller.cancel('call-1', 'Not needed'), isTrue);
    expect(service.directCancellations, 1);
    controller.dispose();
  });

  test(
    'no cancel is offered while a request is pending or the crew has arrived',
    () async {
      final service = _FakeService()
        ..tracking = const MyCallTracking(
          callStatus: CallStatus.dispatched,
          dispatchStatus: DispatchStatus.enRouteToScene,
          cancellationRequestStatus: CancellationRequestStatus.pending,
        );
      final controller = PatientEmergencyController(service);
      await controller.refreshTracking('call-1');
      expect(controller.canCancel, isFalse);

      service.tracking = const MyCallTracking(
        callStatus: CallStatus.enRoute,
        dispatchStatus: DispatchStatus.atScene,
      );
      await controller.refreshTracking('call-1');
      expect(controller.canCancel, isFalse);
      expect(await controller.cancel('call-1', 'Too late'), isFalse);
      expect(service.reviewedCancellations + service.directCancellations, 0);
      controller.dispose();
    },
  );

  test(
    'a pending request does not block cancelling once no ambulance is coming',
    () async {
      final service = _FakeService()
        ..tracking = const MyCallTracking(
          callStatus: CallStatus.received,
          lookingForAnotherAmbulance: true,
          cancellationRequestStatus: CancellationRequestStatus.pending,
        );
      final controller = PatientEmergencyController(service);
      await controller.refreshTracking('call-1');

      expect(controller.canCancel, isTrue);
      expect(controller.cancelNeedsReview, isFalse);
      expect(await controller.cancel('call-1', 'Got a lift'), isTrue);
      expect(service.directCancellations, 1);
      controller.dispose();
    },
  );

  test('points the caller to a request that is still open', () async {
    final service = _FakeService()
      ..summaries = const [
        MyEmergencyCallSummary(id: 'old', status: CallStatus.completed),
        MyEmergencyCallSummary(id: 'open', status: CallStatus.dispatched),
      ];
    final controller = PatientEmergencyController(service);
    await controller.load();

    expect(controller.openCall?.id, 'open');
    controller.dispose();
  });
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
  List<MyEmergencyCallSummary> summaries = const [];
  MyCallTracking tracking = const MyCallTracking(
    emergencyCallId: 'call-1',
    callStatus: CallStatus.received,
  );

  @override
  Future<EmergencyCallDetail> report(CreateEmergencyCallRequest request) async {
    requests.add(request);
    if (reportFailures-- > 0) {
      throw const ApiException(message: 'offline');
    }
    return const EmergencyCallDetail(id: 'call-1');
  }

  @override
  Future<List<MyEmergencyCallSummary>> calls() async => summaries;

  @override
  Future<MyCallTracking> track(String id) async => tracking;

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
