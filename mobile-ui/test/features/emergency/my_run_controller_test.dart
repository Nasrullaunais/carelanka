import 'package:carelanka_mobile/core/network/api_exception.dart';
import 'package:carelanka_mobile/core/widgets/async_data.dart';
import 'package:carelanka_mobile/features/emergency/models/handover_draft.dart';
import 'package:carelanka_mobile/features/emergency/models/run_ending.dart';
import 'package:carelanka_mobile/features/emergency/models/run_step.dart';
import 'package:carelanka_mobile/features/emergency/services/crew_location_reporter.dart';
import 'package:carelanka_mobile/features/emergency/services/crew_run_service.dart';
import 'package:carelanka_mobile/features/emergency/state/my_run_controller.dart';
import 'package:carelanka_mobile/services/api_client/models/dispatch_detail.dart';
import 'package:carelanka_mobile/features/emergency/state/run_history_controller.dart';
import 'package:carelanka_mobile/services/api_client/models/dispatch_status.dart';
import 'package:carelanka_mobile/services/api_client/models/dispatch_summary.dart';
import 'package:carelanka_mobile/services/api_client/models/dispatch_summary_paged_result.dart';
import 'package:carelanka_mobile/services/api_client/models/navigation_target.dart';
import 'package:carelanka_mobile/services/api_client/models/scene_outcome.dart';
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
  String? ambulance;
  Object? ambulanceError;
  int ambulanceLookups = 0;
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
  Future<DispatchDetail> advance(
    String id,
    DispatchStatus next, {
    CrewPosition? position,
  }) => _reply(
    'advance:${next.json}${position == null ? '' : '@${position.latitude},${position.longitude}'}',
    next,
  );

  @override
  Future<DispatchDetail> handOver(
    String id, {
    String? notes,
    String? patientCondition,
  }) => _reply('handover:$notes|$patientCondition', DispatchStatus.handedOver);

  @override
  Future<DispatchDetail> endAtScene(
    String id,
    SceneOutcome outcome, {
    String? notes,
  }) =>
      _reply('endAtScene:${outcome.json}|$notes', DispatchStatus.endedAtScene);

  @override
  Future<String?> assignedAmbulanceRegistration() async {
    ambulanceLookups++;
    final error = ambulanceError;
    if (error != null) throw error;
    return ambulance;
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
    expect(DispatchStatus.transportingToHospital.canEndAtScene, isFalse);
    expect(DispatchStatus.atScene.canWriteHandoverNotes, isTrue);
    expect(DispatchStatus.transportingToHospital.canWriteHandoverNotes, isTrue);
    expect(DispatchStatus.enRouteToScene.canWriteHandoverNotes, isFalse);
    expect(DispatchStatus.endedAtScene.canWriteHandoverNotes, isFalse);
    expect(DispatchStatus.endedAtScene.isLive, isFalse);
    expect(DispatchStatus.endedAtScene.nextStep, isNull);
  });

  test('only the two easy-to-hit steps ask to be confirmed', () {
    expect(RunStep.values.where((step) => step.needsConfirmation), [
      RunStep.arrivedAtScene,
      RunStep.leaveForHospital,
    ]);
  });

  test('maps opens when driving starts and when the patient is on board', () {
    expect(
      DispatchStatus.values.where((status) => status.opensNavigationOnEntry),
      [DispatchStatus.enRouteToScene, DispatchStatus.transportingToHospital],
    );
  });

  test('the navigation button names where it goes', () {
    expect(DispatchStatus.acknowledged.navigationLabel, 'Navigate to scene');
    expect(DispatchStatus.enRouteToScene.navigationLabel, 'Navigate to scene');
    expect(DispatchStatus.atScene.navigationLabel, 'Navigate to scene');
    expect(
      DispatchStatus.transportingToHospital.navigationLabel,
      'Navigate to hospital',
    );
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

  test('finishing at the scene sends the reason and clears the run', () async {
    final service = FakeRunService()..active = _run(DispatchStatus.atScene);
    final controller = MyRunController(service);
    await controller.load();

    final done = await controller.endAtScene(
      SceneOutcome.patientRefused,
      notes: 'Wants her own doctor',
    );

    expect(done, isTrue);
    expect(service.calls, ['endAtScene:patient_refused|Wants her own doctor']);
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

  group('where a step happened', () {
    Future<(FakeRunService, MyRunController)> acknowledged(
      CrewPosition? Function() position,
    ) async {
      final service = FakeRunService()
        ..active = _run(DispatchStatus.acknowledged);
      final controller = MyRunController(service, latestPosition: position);
      await controller.load();
      return (service, controller);
    }

    test('sends the latest fix with the step', () async {
      final (service, controller) = await acknowledged(
        () => const CrewPosition(6.9271, 79.8612),
      );

      await controller.advance();

      expect(service.calls, ['advance:en_route_to_scene@6.9271,79.8612']);
    });

    test('sends nothing when there is no recent fix', () async {
      final (service, controller) = await acknowledged(() => null);

      await controller.advance();

      expect(service.calls, ['advance:en_route_to_scene']);
    });

    test('asks for the fix again when a step is retried', () async {
      var fix = const CrewPosition(1, 1);
      final (service, controller) = await acknowledged(() => fix);
      service.nextError = const ApiException(message: 'offline');

      await controller.advance();
      fix = const CrewPosition(2, 2);
      await controller.retry();

      expect(service.calls, [
        'advance:en_route_to_scene@1.0,1.0',
        'advance:en_route_to_scene@2.0,2.0',
      ]);
    });
  });

  group('a reply that never arrived', () {
    const conflict = ApiException(message: 'Not allowed', statusCode: 409);

    test('a step the server already took counts as done', () async {
      final service = FakeRunService()
        ..active = _run(DispatchStatus.acknowledged);
      final controller = MyRunController(service);
      await controller.load();
      service.nextError = conflict;
      service.active = _run(DispatchStatus.enRouteToScene);

      final done = await controller.advance();

      expect(done, isTrue);
      expect(controller.actionError, isNull);
      expect(controller.canRetry, isFalse);
      expect(
        controller.state.valueOrNull?.status,
        DispatchStatus.enRouteToScene,
      );
    });

    test('accepting a run that was already accepted counts as done', () async {
      final service = FakeRunService()..active = _run(DispatchStatus.assigned);
      final controller = MyRunController(service);
      await controller.load();
      service.nextError = conflict;
      service.active = _run(DispatchStatus.acknowledged);

      expect(await controller.acknowledge(), isTrue);
      expect(controller.actionError, isNull);
    });

    test('a handover that was already recorded counts as done', () async {
      final service = FakeRunService()
        ..active = _run(DispatchStatus.transportingToHospital);
      final controller = MyRunController(service);
      await controller.load();
      service.nextError = conflict;
      service.active = null;
      service.ended['run-1'] = _ended(DispatchStatus.handedOver);

      final done = await controller.handOver();

      expect(done, isTrue);
      expect(controller.actionError, isNull);
      expect(controller.ending?.kind, RunEndingKind.handedOver);
      expect(
        service.calls.where((call) => call.startsWith('getRun')),
        hasLength(1),
      );
    });

    test('a decline that was already recorded counts as done', () async {
      final service = FakeRunService()..active = _run(DispatchStatus.assigned);
      final controller = MyRunController(service);
      await controller.load();
      service.nextError = conflict;
      service.active = null;
      service.ended['run-1'] = _ended(DispatchStatus.declined);

      expect(await controller.decline('Flat tyre'), isTrue);
      expect(controller.actionError, isNull);
      expect(controller.ending, isNull);
    });

    test('a run that ended some other way is not called done', () async {
      final service = FakeRunService()
        ..active = _run(DispatchStatus.transportingToHospital);
      final controller = MyRunController(service);
      await controller.load();
      service.nextError = conflict;
      service.active = null;
      service.ended['run-1'] = _ended(DispatchStatus.cancelled);

      expect(await controller.handOver(), isFalse);
      expect(controller.ending?.kind, RunEndingKind.cancelled);
    });

    test('a run that has not reached the asked state is not done', () async {
      final service = FakeRunService()
        ..active = _run(DispatchStatus.enRouteToScene);
      final controller = MyRunController(service);
      await controller.load();
      service.nextError = conflict;

      expect(await controller.advance(), isFalse);
      expect(controller.actionError?.message, 'Not allowed');
    });
  });

  group('when there is no signal', () {
    const offline = ApiException(message: 'offline');

    Future<(FakeRunService, MyRunController)> enRoute() async {
      final service = FakeRunService()
        ..active = _run(DispatchStatus.enRouteToScene);
      final controller = MyRunController(service);
      await controller.load();
      return (service, controller);
    }

    test('the step is not saved and can be tried again', () async {
      final (service, controller) = await enRoute();
      service.nextError = offline;

      expect(await controller.advance(), isFalse);
      expect(controller.canRetry, isTrue);
      expect(
        controller.state.valueOrNull?.status,
        DispatchStatus.enRouteToScene,
      );

      expect(await controller.retry(), isTrue);
      expect(service.calls, ['advance:at_scene', 'advance:at_scene']);
      expect(controller.state.valueOrNull?.status, DispatchStatus.atScene);
      expect(controller.canRetry, isFalse);
      expect(controller.actionError, isNull);
    });

    test('trying again repeats the same handover details', () async {
      final service = FakeRunService()
        ..active = _run(DispatchStatus.transportingToHospital);
      final controller = MyRunController(service);
      await controller.load();
      service.nextError = offline;

      await controller.handOver(notes: 'Stable', patientCondition: 'Awake');
      await controller.retry();

      expect(service.calls, ['handover:Stable|Awake', 'handover:Stable|Awake']);
    });

    test('a server refusal cannot be retried', () async {
      final (service, controller) = await enRoute();
      service.nextError = const ApiException(
        message: 'Forbidden',
        statusCode: 403,
      );

      await controller.advance();

      expect(controller.canRetry, isFalse);
      expect(await controller.retry(), isFalse);
    });

    test('dismissing the message drops the retry', () async {
      final (service, controller) = await enRoute();
      service.nextError = offline;
      await controller.advance();

      controller.clearActionError();

      expect(controller.canRetry, isFalse);
      expect(await controller.retry(), isFalse);
      expect(service.calls, ['advance:at_scene']);
    });

    test(
      'a retry never takes a further step than the one that failed',
      () async {
        final (service, controller) = await enRoute();
        service.nextError = offline;
        await controller.advance();
        service.active = _run(DispatchStatus.atScene);
        await controller.load(showLoading: false);
        service.nextError = const ApiException(
          message: 'Not allowed',
          statusCode: 409,
        );

        final done = await controller.retry();

        expect(done, isTrue);
        expect(service.calls, ['advance:at_scene', 'advance:at_scene']);
        expect(controller.state.valueOrNull?.status, DispatchStatus.atScene);
      },
    );

    test('the retry is dropped once the run has changed', () async {
      final (service, controller) = await enRoute();
      service.nextError = offline;
      await controller.advance();
      service.active = _run(DispatchStatus.assigned, id: 'run-2');
      service.ended['run-1'] = _ended(DispatchStatus.cancelled);

      await controller.load(showLoading: false);

      expect(controller.canRetry, isFalse);
      expect(controller.actionError, isNull);
      expect(await controller.retry(), isFalse);
    });
  });

  group('the ambulance shown while there is no run', () {
    test('is looked up once the crew has no run', () async {
      final service = FakeRunService()..ambulance = 'AMB-3';
      final controller = MyRunController(service);

      await controller.load();

      expect(controller.ambulance.valueOrNull, 'AMB-3');
    });

    test('is empty for someone who is not on an ambulance crew', () async {
      final controller = MyRunController(FakeRunService());

      await controller.load();

      expect(controller.ambulance, isA<AsyncReady<String?>>());
      expect(controller.ambulance.valueOrNull, isNull);
    });

    test('is not looked up while a run is on screen', () async {
      final service = FakeRunService()
        ..ambulance = 'AMB-3'
        ..active = _run(DispatchStatus.assigned);
      final controller = MyRunController(service);

      await controller.load();
      await controller.load(showLoading: false);

      expect(service.ambulanceLookups, 0);
    });

    test('keeps the last known ambulance when a refresh fails', () async {
      final service = FakeRunService()..ambulance = 'AMB-3';
      final controller = MyRunController(service);
      await controller.load();
      service.ambulanceError = const ApiException(message: 'offline');

      await controller.load(showLoading: false);

      expect(controller.ambulance.valueOrNull, 'AMB-3');
    });

    test('is unknown, not "unassigned", when the first lookup fails', () async {
      final service = FakeRunService()
        ..ambulanceError = const ApiException(message: 'offline');
      final controller = MyRunController(service);

      await controller.load();

      expect(controller.ambulance, isA<AsyncFailed<String?>>());
    });
  });

  group('the handover draft', () {
    const draft = HandoverDraft(
      patientCondition: 'Conscious',
      notes: 'Left leg splinted',
    );

    Future<(FakeRunService, MyRunController)> atScene() async {
      final service = FakeRunService()..active = _run(DispatchStatus.atScene);
      final controller = MyRunController(service);
      await controller.load();
      return (service, controller);
    }

    test('starts empty', () async {
      final (_, controller) = await atScene();

      expect(controller.handoverDraft.isEmpty, isTrue);
    });

    test('is kept while the run goes on, even across reloads', () async {
      final (_, controller) = await atScene();

      controller.saveHandoverDraft(draft);
      await controller.load(showLoading: false);
      await controller.advance();

      expect(controller.handoverDraft.notes, 'Left leg splinted');
      expect(controller.handoverDraft.patientCondition, 'Conscious');
    });

    test('is thrown away once the handover is recorded', () async {
      final (_, controller) = await atScene();
      controller.saveHandoverDraft(draft);

      await controller.handOver(notes: 'Left leg splinted');

      expect(controller.handoverDraft.isEmpty, isTrue);
    });

    test('is thrown away when the run ends another way', () async {
      final (_, controller) = await atScene();
      controller.saveHandoverDraft(draft);

      await controller.endAtScene(SceneOutcome.treatedAtScene);

      expect(controller.handoverDraft.isEmpty, isTrue);
    });

    test('does not carry over to the run that replaces it', () async {
      final (service, controller) = await atScene();
      controller.saveHandoverDraft(draft);
      service.active = _run(DispatchStatus.assigned, id: 'run-2');
      service.ended['run-1'] = _ended(
        DispatchStatus.reassigned,
        supersededBy: 'run-2',
      );

      await controller.load(showLoading: false);

      expect(controller.state.valueOrNull?.id, 'run-2');
      expect(controller.handoverDraft.isEmpty, isTrue);
    });

    test(
      'only redraws the screen when the button label would change',
      () async {
        final (_, controller) = await atScene();
        var redraws = 0;
        controller.addListener(() => redraws++);

        controller.saveHandoverDraft(const HandoverDraft(notes: 'S'));
        controller.saveHandoverDraft(const HandoverDraft(notes: 'Sp'));
        controller.saveHandoverDraft(const HandoverDraft(notes: 'Spl'));
        controller.saveHandoverDraft(HandoverDraft.empty);

        expect(redraws, 2);
      },
    );

    test('counts blank text as nothing written', () {
      const blank = HandoverDraft(patientCondition: '  ', notes: '\n');

      expect(blank.isEmpty, isTrue);
      expect(blank.notesOrNull, isNull);
      expect(blank.patientConditionOrNull, isNull);
      expect(const HandoverDraft(notes: ' hello ').notesOrNull, 'hello');
    });
  });

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

    test('finishing at the scene is confirmed without asking again', () async {
      final service = FakeRunService()..active = _run(DispatchStatus.atScene);
      final controller = MyRunController(service);
      await controller.load();

      await controller.endAtScene(SceneOutcome.falseAlarm);

      expect(controller.ending?.kind, RunEndingKind.endedAtScene);
      expect(controller.ending?.title, 'Run finished at the scene');
      expect(service.calls.where((call) => call.startsWith('getRun')), isEmpty);
    });

    test('a run your partner finished at the scene explains itself', () async {
      final (service, controller) = await liveRun();
      service.active = null;
      service.ended['run-1'] = _ended(DispatchStatus.endedAtScene);

      await controller.load(showLoading: false);

      expect(controller.ending?.kind, RunEndingKind.endedAtScene);
      expect(
        controller.ending?.message,
        'AMB-3 is available for the next run.',
      );
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
