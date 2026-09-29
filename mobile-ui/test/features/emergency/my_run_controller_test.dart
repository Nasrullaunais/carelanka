import 'package:carelanka_mobile/core/network/api_exception.dart';
import 'package:carelanka_mobile/core/widgets/async_data.dart';
import 'package:carelanka_mobile/features/emergency/models/run_ending.dart';
import 'package:carelanka_mobile/features/emergency/models/run_step.dart';
import 'package:carelanka_mobile/features/emergency/services/crew_run_service.dart';
import 'package:carelanka_mobile/features/emergency/state/my_run_controller.dart';
import 'package:carelanka_mobile/services/api_client/models/dispatch_detail.dart';
import 'package:carelanka_mobile/features/emergency/state/run_history_controller.dart';
import 'package:carelanka_mobile/services/api_client/models/dispatch_status.dart';
import 'package:carelanka_mobile/services/api_client/models/dispatch_summary.dart';
import 'package:carelanka_mobile/services/api_client/models/dispatch_summary_paged_result.dart';
import 'package:carelanka_mobile/services/api_client/models/navigation_target.dart';
import 'package:flutter_test/flutter_test.dart';

DispatchDetail _run(DispatchStatus status, {String id = 'run-1'}) =>
    DispatchDetail(id: id, status: status);

DispatchDetail _ended(
  DispatchStatus status, {
  String? cancellationReason,
  String? reassignmentReason,
  String? supersededBy,
}) => DispatchDetail(
  id: 'run-1',
  status: status,
  ambulanceRegistration: 'AMB-3',
  cancellationReason: cancellationReason,
  reassignmentReason: reassignmentReason,
  supersededByDispatchId: supersededBy,
  completedAt: DateTime.utc(2026, 9, 30, 5, 22),
);

final class FakeRunService implements CrewRunService {
  DispatchDetail? active;
  Object? nextError;
  final ended = <String, DispatchDetail>{};
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
  Future<DispatchDetail> getRun(String id) async {
    calls.add('getRun:$id');
    return ended[id] ??
        (throw const ApiException(message: 'offline', statusCode: null));
  }

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
  });

  test(
    'a crew member with no live run sees an empty state, not an error',
    () async {
      final controller = MyRunController(FakeRunService());

      await controller.load();

      expect((controller.state as AsyncReady<DispatchDetail?>).value, isNull);
    },
  );

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

  test('a conflict explains itself through the ending panel', () async {
    final service = FakeRunService()
      ..active = _run(DispatchStatus.acknowledged);
    final controller = MyRunController(service);
    await controller.load();
    service.nextError = const ApiException(
      message: 'Run was cancelled',
      statusCode: 409,
    );
    service.active = null;
    service.ended['run-1'] = _ended(DispatchStatus.cancelled);

    final done = await controller.advance();

    expect(done, isFalse);
    expect(controller.actionError, isNull);
    expect(controller.ending?.kind, RunEndingKind.cancelled);
    expect(controller.state.valueOrNull, isNull);
  });

  test('a conflict with the run still live keeps the server message', () async {
    final service = FakeRunService()
      ..active = _run(DispatchStatus.acknowledged);
    final controller = MyRunController(service);
    await controller.load();
    service.nextError = const ApiException(
      message: 'Not allowed now',
      statusCode: 409,
    );

    await controller.advance();

    expect(controller.actionError?.message, 'Not allowed now');
    expect(controller.ending, isNull);
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

  group('when a run ends', () {
    Future<(FakeRunService, MyRunController)> liveRun() async {
      final service = FakeRunService()
        ..active = _run(DispatchStatus.enRouteToScene);
      final controller = MyRunController(service);
      await controller.load();
      return (service, controller);
    }

    test('nothing is shown for a run the app never saw', () async {
      final service = FakeRunService();
      final controller = MyRunController(service);

      await controller.load();

      expect(controller.ending, isNull);
      expect(service.calls, isEmpty);
    });

    test('a cancelled run says why', () async {
      final (service, controller) = await liveRun();
      service.active = null;
      service.ended['run-1'] = _ended(
        DispatchStatus.cancelled,
        cancellationReason: ' Caller called back ',
      );

      await controller.load(showLoading: false);

      final ending = controller.ending!;
      expect(ending.kind, RunEndingKind.cancelled);
      expect(ending.reason, 'Caller called back');
      expect(ending.message, contains('You can stop driving.'));
      expect(controller.state.valueOrNull, isNull);
    });

    test('a run given to another ambulance shows the reason', () async {
      final (service, controller) = await liveRun();
      service.active = null;
      service.ended['run-1'] = _ended(
        DispatchStatus.reassigned,
        reassignmentReason: 'Closer ambulance became free',
      );

      await controller.load(showLoading: false);

      expect(controller.ending?.kind, RunEndingKind.reassigned);
      expect(
        controller.ending?.message,
        'Reason: Closer ambulance became free',
      );
    });

    test('a diversion is told apart and the new run stays visible', () async {
      final (service, controller) = await liveRun();
      service.active = _run(DispatchStatus.assigned, id: 'run-2');
      service.ended['run-1'] = _ended(
        DispatchStatus.reassigned,
        supersededBy: 'run-2',
      );

      await controller.load(showLoading: false);

      expect(controller.ending?.kind, RunEndingKind.diverted);
      expect(controller.state.valueOrNull?.id, 'run-2');
    });

    test('a reassignment to a different run is not a diversion', () async {
      final (service, controller) = await liveRun();
      service.active = _run(DispatchStatus.assigned, id: 'run-3');
      service.ended['run-1'] = _ended(
        DispatchStatus.reassigned,
        supersededBy: 'run-2',
      );

      await controller.load(showLoading: false);

      expect(controller.ending?.kind, RunEndingKind.reassigned);
    });

    test('a handover is confirmed without asking the server again', () async {
      final (service, controller) = await liveRun();

      await controller.handOver();

      expect(controller.ending?.kind, RunEndingKind.handedOver);
      expect(service.calls.where((call) => call.startsWith('getRun')), isEmpty);
    });

    test('declining a run leaves no panel', () async {
      final service = FakeRunService()..active = _run(DispatchStatus.assigned);
      final controller = MyRunController(service);
      await controller.load();

      await controller.decline('Flat tyre');

      expect(controller.ending, isNull);
    });

    test('never invents a reason when the details cannot be fetched', () async {
      final (service, controller) = await liveRun();
      service.active = null;

      await controller.load(showLoading: false);

      expect(controller.ending?.kind, RunEndingKind.unavailable);
      expect(controller.ending?.title, 'This run is no longer assigned to you');
    });

    test('the panel survives refreshes until it is dismissed', () async {
      final (service, controller) = await liveRun();
      service.active = null;
      service.ended['run-1'] = _ended(DispatchStatus.cancelled);
      await controller.load(showLoading: false);

      await controller.load(showLoading: false);
      expect(controller.ending?.kind, RunEndingKind.cancelled);
      expect(
        service.calls.where((call) => call.startsWith('getRun')),
        hasLength(1),
      );

      controller.dismissEnding();
      expect(controller.ending, isNull);
    });

    test('a new run does not clear the previous ending', () async {
      final (service, controller) = await liveRun();
      service.active = null;
      service.ended['run-1'] = _ended(DispatchStatus.handedOver);
      await controller.load(showLoading: false);

      service.active = _run(DispatchStatus.assigned, id: 'run-2');
      await controller.load(showLoading: false);

      expect(controller.ending?.kind, RunEndingKind.handedOver);
      expect(controller.state.valueOrNull?.id, 'run-2');
    });
  });

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
