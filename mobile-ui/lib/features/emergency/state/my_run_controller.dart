import 'dart:async';

import 'package:flutter/foundation.dart';

import '../../../core/network/api_exception.dart';
import '../../../core/widgets/async_data.dart';
import '../../../services/api_client/models/dispatch_detail.dart';
import '../../../services/api_client/models/dispatch_status.dart';
import '../../../services/api_client/models/navigation_target.dart';
import '../../../services/api_client/models/scene_outcome.dart';
import '../models/handover_draft.dart';
import '../models/run_ending.dart';
import '../models/run_step.dart';
import '../services/crew_location_reporter.dart';
import '../services/crew_run_service.dart';

typedef LatestPosition = CrewPosition? Function();

/// One crew tap, tied to the run it was made on so a retry can never land on another run.
final class _RunAction {
  const _RunAction(this.runId, this.target, this.send);

  final String runId;
  final DispatchStatus target;
  final Future<DispatchDetail> Function() send;
}

class MyRunController extends ChangeNotifier {
  MyRunController(
    this._service, {
    this.pollInterval = const Duration(seconds: 10),
    LatestPosition? latestPosition,
  }) : _latestPosition = latestPosition;

  final CrewRunService _service;
  final Duration pollInterval;
  final LatestPosition? _latestPosition;
  Timer? _poll;
  bool _disposed = false;

  AsyncData<DispatchDetail?> _state = const AsyncData.loading();
  String? _liveRunId;
  RunEnding? _ending;
  HandoverDraft _handoverDraft = HandoverDraft.empty;
  bool _busy = false;
  ApiException? _actionError;
  _RunAction? _unsavedAction;
  AsyncData<String?> _ambulance = const AsyncData.loading();

  AsyncData<DispatchDetail?> get state => _state;
  RunEnding? get ending => _ending;
  HandoverDraft get handoverDraft => _handoverDraft;
  bool get busy => _busy;
  ApiException? get actionError => _actionError;
  bool get canRetry => _unsavedAction != null;
  AsyncData<String?> get ambulance => _ambulance;

  void startPolling() {
    _poll?.cancel();
    _poll = Timer.periodic(pollInterval, (_) {
      if (!_busy) load(showLoading: false);
    });
    load();
  }

  Future<void> load({bool showLoading = true}) async {
    if (showLoading) {
      _state = const AsyncData.loading();
      notifyListeners();
    }
    try {
      await _show(await _service.activeRun());
    } on ApiException catch (error) {
      if (showLoading || _state is! AsyncReady<DispatchDetail?>) {
        _state = AsyncData.failed(error);
      }
    }
    _notify();
    if (_liveRunId == null) await _loadAmbulance();
  }

  Future<void> _loadAmbulance() async {
    try {
      _ambulance = AsyncData.ready(
        await _service.assignedAmbulanceRegistration(),
      );
    } on ApiException catch (error) {
      if (_ambulance is! AsyncReady<String?>) {
        _ambulance = AsyncData.failed(error);
      }
    }
    _notify();
  }

  void saveHandoverDraft(HandoverDraft draft) {
    final changesButton = draft.isEmpty != _handoverDraft.isEmpty;
    _handoverDraft = draft;
    if (changesButton) _notify();
  }

  void dismissEnding() {
    _ending = null;
    _notify();
  }

  Future<bool> acknowledge() =>
      _begin(DispatchStatus.acknowledged, _service.acknowledge);

  Future<bool> decline(String reason) =>
      _begin(DispatchStatus.declined, (id) => _service.decline(id, reason));

  Future<bool> advance() {
    final next = _state.valueOrNull?.status?.nextStep?.nextStatus;
    if (next == null) return Future.value(false);
    return _begin(
      next,
      (id) => _service.advance(id, next, position: _latestPosition?.call()),
    );
  }

  Future<bool> handOver({String? notes, String? patientCondition}) => _begin(
    DispatchStatus.handedOver,
    (id) =>
        _service.handOver(id, notes: notes, patientCondition: patientCondition),
  );

  Future<bool> endAtScene(SceneOutcome outcome, {String? notes}) => _begin(
    DispatchStatus.endedAtScene,
    (id) => _service.endAtScene(id, outcome, notes: notes),
  );

  Future<bool> retry() {
    final action = _unsavedAction;
    return action == null ? Future.value(false) : _act(action);
  }

  Future<NavigationTarget?> navigationTarget() async {
    final run = _state.valueOrNull;
    if (run == null) return null;
    try {
      return await _service.navigationTarget(run.id!);
    } on ApiException catch (error) {
      _actionError = error;
      _notify();
      return null;
    }
  }

  void clearActionError() {
    _actionError = null;
    _unsavedAction = null;
    _notify();
  }

  Future<bool> _begin(
    DispatchStatus target,
    Future<DispatchDetail> Function(String runId) send,
  ) {
    final runId = _state.valueOrNull?.id;
    if (runId == null) return Future.value(false);
    return _act(_RunAction(runId, target, () => send(runId)));
  }

  Future<bool> _act(_RunAction action) async {
    if (_busy || _liveRunId != action.runId) return false;
    _busy = true;
    _actionError = null;
    _unsavedAction = null;
    notifyListeners();
    try {
      await _show(await action.send());
      return true;
    } on ApiException catch (error) {
      _actionError = error;
      if (error.isNetworkFailure) {
        _unsavedAction = action;
        return false;
      }
      if (!error.isConflict && !error.isNotFound) return false;
      final landed = await _reconcile(action);
      if (landed) _actionError = null;
      return landed;
    } finally {
      _busy = false;
      _notify();
    }
  }

  // A 409 usually means the run moved under us. Returns true when it already reached the state the tap asked for, e.g. the first reply was lost.
  Future<bool> _reconcile(_RunAction action) async {
    try {
      final active = await _service.activeRun();
      final tracked = active?.id == action.runId
          ? active
          : await _fetchRun(action.runId);
      await _show(active, ended: tracked);
      return tracked?.status == action.target;
    } on ApiException {
      return false;
    }
  }

  Future<DispatchDetail?> _fetchRun(String id) async {
    try {
      return await _service.getRun(id);
    } on ApiException {
      return null;
    }
  }

  Future<void> _show(DispatchDetail? run, {DispatchDetail? ended}) async {
    final live = run != null && (run.status?.isLive ?? false) ? run : null;
    final endedRunId = _liveRunId;
    if (endedRunId != null && endedRunId != live?.id) {
      final ending = await _endingOf(
        endedRunId,
        live?.id,
        knownEnd: ended ?? run,
      );
      if (ending != null) _ending = ending;
    }
    if (_liveRunId != live?.id) {
      _handoverDraft = HandoverDraft.empty;
      _actionError = null;
      _unsavedAction = null;
    }
    _liveRunId = live?.id;
    _state = AsyncData.ready(live);
  }

  Future<RunEnding?> _endingOf(
    String runId,
    String? activeRunId, {
    DispatchDetail? knownEnd,
  }) async {
    try {
      final ended = knownEnd?.id == runId
          ? knownEnd!
          : await _service.getRun(runId);
      return RunEnding.from(ended, activeRunId: activeRunId);
    } on ApiException {
      return const RunEnding.unavailable();
    }
  }

  void _notify() {
    if (!_disposed) notifyListeners();
  }

  @override
  void dispose() {
    _disposed = true;
    _poll?.cancel();
    super.dispose();
  }
}
