import 'package:carelanka_mobile/core/network/api_exception.dart';
import 'package:carelanka_mobile/core/widgets/async_data.dart';
import 'package:carelanka_mobile/features/emergency/models/run_step.dart';
import 'package:carelanka_mobile/features/emergency/services/crew_run_service.dart';
import 'package:carelanka_mobile/features/emergency/state/my_run_controller.dart';
import 'package:carelanka_mobile/services/api_client/models/dispatch_detail.dart';
import 'package:carelanka_mobile/features/emergency/state/run_history_controller.dart';
import 'package:carelanka_mobile/services/api_client/models/dispatch_status.dart';
import 'package:carelanka_mobile/services/api_client/models/dispatch_summary.dart';
import 'package:carelanka_mobile/services/api_client/models/dispatch_summary_paged_result.dart';
import 'package:carelanka_mobile/services/api_client/models/emergency_call_outcome.dart';
import 'package:carelanka_mobile/services/api_client/models/navigation_target.dart';
import 'package:flutter_test/flutter_test.dart';

DispatchDetail _run(DispatchStatus status) =>
    DispatchDetail(id: 'run-1', status: status);

final class FakeRunService implements CrewRunService {
  DispatchDetail? active;
  DispatchDetail? finished;
  bool onAmbulance = true;
  Object? nextError;
  final calls = <String>[];

  Future<DispatchDetail> _reply(String call, DispatchStatus status) async {
    calls.add(call);
    final error = nextError;
    if (error != null) {
      nextError = null;
      throw error;
    }
    return active = _run(status);
  }

  @override
  Future<DispatchDetail?> activeRun() async => active;

  @override
  Future<DispatchDetail> acknowledge(String id) =>
      _reply('acknowledge', DispatchStatus.acknowledged);

  @override
  Future<DispatchDetail> decline(String id, String reason) =>
      _reply('decline:$reason', DispatchStatus.declined);

  @override
  Future<DispatchDetail> advance(String id, DispatchStatus next) =>
      _reply('advance:${next.json}', next);

  @override
  Future<DispatchDetail> handOver(
    String id, {
    String? notes,
    String? patientCondition,
  }) => _reply('handover:$notes|$patientCondition', DispatchStatus.handedOver);

  @override
  Future<DispatchDetail> closeAtScene(
    String id,
    EmergencyCallOutcome outcome, {
    String? notes,
  }) => _reply('close:${outcome.json}|$notes', DispatchStatus.closedAtScene);

  @override
  Future<DispatchDetail> run(String id) async {
    calls.add('run:$id');
    return finished ?? _run(DispatchStatus.handedOver);
  }

  @override
  Future<DispatchSummaryPagedResult> history({required int page}) async {
    calls.add('history:$page');
    return DispatchSummaryPagedResult(
      items: [
        DispatchSummary(id: 'past-$page', status: DispatchStatus.handedOver),
      ],
      page: page,
      pageSize: 1,
      totalItems: 2,
      totalPages: 2,
    );
  }

  @override
  Future<NavigationTarget> navigationTarget(String id) async =>
      const NavigationTarget(googleMapsUrl: 'https://maps');

  @override
  Future<bool> onAmbulanceCrew() async => onAmbulance;
}

void main() {
  test('only the next legal step is offered at each stage', () {
    expect(DispatchStatus.assigned.nextStep, RunStep.acknowledge);
    expect(DispatchStatus.acknowledged.nextStep, RunStep.startDriving);
    expect(DispatchStatus.enRouteToScene.nextStep, RunStep.arrivedAtScene);
    expect(DispatchStatus.atScene.nextStep, RunStep.leaveForHospital);
    expect(DispatchStatus.transportingToHospital.nextStep, RunStep.handOver);
    expect(DispatchStatus.handedOver.nextStep, isNull);
    expect(DispatchStatus.assigned.canDecline, isTrue);
    expect(DispatchStatus.acknowledged.canDecline, isFalse);
    expect(DispatchStatus.assigned.canNavigate, isFalse);
    expect(DispatchStatus.atScene.canNavigate, isTrue);
    expect(DispatchStatus.atScene.canEndAtScene, isTrue);
    expect(DispatchStatus.enRouteToScene.canEndAtScene, isFalse);
  });

  test(
    'ending at the scene records the outcome and says the run is over',
    () async {
      final service = FakeRunService()..active = _run(DispatchStatus.atScene);
      final controller = MyRunController(service);
      await controller.load();

      final done = await controller.closeAtScene(
        EmergencyCallOutcome.refusedTransport,
        notes: 'Signed refusal',
      );

      expect(done, isTrue);
      expect(service.calls, ['close:refused_transport|Signed refusal']);
      expect(controller.state.valueOrNull, isNull);
      expect(controller.endedRun?.status, DispatchStatus.closedAtScene);
    },
  );

  test('a run called off elsewhere is explained, not just removed', () async {
    final service = FakeRunService()
      ..active = _run(DispatchStatus.enRouteToScene);
    final controller = MyRunController(service);
    await controller.load();
    service
      ..active = null
      ..finished = const DispatchDetail(
        id: 'run-1',
        status: DispatchStatus.cancelled,
        cancellationReason: 'Caller found a taxi',
      );

    await controller.load(showLoading: false);

    expect(service.calls, ['run:run-1']);
    expect(controller.state.valueOrNull, isNull);
    expect(controller.endedRun?.cancellationReason, 'Caller found a taxi');

    controller.dismissEndedRun();
    expect(controller.endedRun, isNull);
  });

  test('a new run replaces the message about the last one', () async {
    final service = FakeRunService()..active = _run(DispatchStatus.atScene);
    final controller = MyRunController(service);
    await controller.load();
    await controller.closeAtScene(EmergencyCallOutcome.treatedAtScene);
    service.active = const DispatchDetail(
      id: 'run-2',
      status: DispatchStatus.assigned,
    );

    await controller.load(showLoading: false);

    expect(controller.endedRun, isNull);
    expect(controller.state.valueOrNull?.id, 'run-2');
  });

  test(
    'a crew member with no live run sees an empty state, not an error',
    () async {
      final controller = MyRunController(FakeRunService());

      await controller.load();

      expect((controller.state as AsyncReady<DispatchDetail?>).value, isNull);
      expect(controller.onAmbulance, isTrue);
    },
  );

  test('a crew member on no ambulance is told so instead of waiting', () async {
    final service = FakeRunService()..onAmbulance = false;
    final controller = MyRunController(service);

    await controller.load();
    expect(controller.onAmbulance, isFalse);

    service.onAmbulance = true;
    await controller.load(showLoading: false);
    expect(controller.onAmbulance, isTrue);
  });

  test('advancing walks the run forward one legal step at a time', () async {
    final service = FakeRunService()
      ..active = _run(DispatchStatus.acknowledged);
    final controller = MyRunController(service);
    await controller.load();

    await controller.advance();
    await controller.advance();
    await controller.advance();

    expect(service.calls, [
      'advance:en_route_to_scene',
      'advance:at_scene',
      'advance:transporting_to_hospital',
    ]);
    expect(
      controller.state.valueOrNull?.status,
      DispatchStatus.transportingToHospital,
    );
  });

  test('handover sends the details and clears the run', () async {
    final service = FakeRunService()
      ..active = _run(DispatchStatus.transportingToHospital);
    final controller = MyRunController(service);
    await controller.load();

    final done = await controller.handOver(
      notes: 'Handed to triage',
      patientCondition: 'Stable',
    );

    expect(done, isTrue);
    expect(service.calls, ['handover:Handed to triage|Stable']);
    expect(controller.state.valueOrNull, isNull);
  });

  test('declining clears the run', () async {
    final service = FakeRunService()..active = _run(DispatchStatus.assigned);
    final controller = MyRunController(service);
    await controller.load();

    await controller.decline('Flat tyre');

    expect(service.calls, ['decline:Flat tyre']);
    expect(controller.state.valueOrNull, isNull);
  });

  test('a conflict shows the server message and re-reads the run', () async {
    final service = FakeRunService()
      ..active = _run(DispatchStatus.acknowledged);
    final controller = MyRunController(service);
    await controller.load();
    service.nextError = const ApiException(
      message: 'Run was cancelled',
      statusCode: 409,
    );
    service.active = null;

    final done = await controller.advance();

    expect(done, isFalse);
    expect(controller.actionError?.message, 'Run was cancelled');
    expect(controller.state.valueOrNull, isNull);
  });

  test(
    'a network failure keeps the run on screen and reports the error',
    () async {
      final service = FakeRunService()..active = _run(DispatchStatus.assigned);
      final controller = MyRunController(service);
      await controller.load();
      service.nextError = const ApiException(message: 'offline');

      await controller.acknowledge();

      expect(controller.actionError?.isNetworkFailure, isTrue);
      expect(controller.state.valueOrNull?.status, DispatchStatus.assigned);
    },
  );

  test(
    'history loads the first page, then older runs on request, then stops',
    () async {
      final controller = RunHistoryController(FakeRunService());
      await controller.load();

      expect(controller.state.valueOrNull?.items.map((run) => run.id), [
        'past-1',
      ]);
      expect(controller.state.valueOrNull?.hasMore, isTrue);

      await controller.loadMore();

      expect(controller.state.valueOrNull?.items.map((run) => run.id), [
        'past-1',
        'past-2',
      ]);
      expect(controller.state.valueOrNull?.hasMore, isFalse);
    },
  );
}
