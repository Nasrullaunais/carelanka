import 'dart:async';

import 'package:flutter/foundation.dart';

import '../../../core/network/api_exception.dart';
import '../../../core/widgets/async_data.dart';
import '../../../services/api_client/models/dispatch_detail.dart';
import '../../../services/api_client/models/navigation_target.dart';
import '../../../services/api_client/models/scene_outcome.dart';
import '../models/handover_draft.dart';
import '../models/run_ending.dart';
import '../models/run_step.dart';
import '../services/crew_run_service.dart';

class MyRunController extends ChangeNotifier {
  MyRunController(
    this._service, {
    this.pollInterval = const Duration(seconds: 10),
  });

  final CrewRunService _service;
  final Duration pollInterval;
  Timer? _poll;
  bool _disposed = false;

  AsyncData<DispatchDetail?> _state = const AsyncData.loading();
  String? _liveRunId;
  RunEnding? _ending;
  HandoverDraft _handoverDraft = HandoverDraft.empty;
  bool _busy = false;
  ApiException? _actionError;

  AsyncData<DispatchDetail?> get state => _state;
  RunEnding? get ending => _ending;
  HandoverDraft get handoverDraft => _handoverDraft;
  bool get busy => _busy;
  ApiException? get actionError => _actionError;

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

  Future<bool> acknowledge() => _act((run) => _service.acknowledge(run.id!));

  Future<bool> decline(String reason) =>
      _act((run) => _service.decline(run.id!, reason));

  Future<bool> advance() => _act((run) {
    final next = run.status?.nextStep?.nextStatus;
    return next == null ? Future.value(run) : _service.advance(run.id!, next);
  });

  Future<bool> handOver({String? notes, String? patientCondition}) => _act(
    (run) => _service.handOver(
      run.id!,
      notes: notes,
      patientCondition: patientCondition,
    ),
  );

  Future<bool> endAtScene(SceneOutcome outcome, {String? notes}) =>
      _act((run) => _service.endAtScene(run.id!, outcome, notes: notes));

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
    _notify();
  }

  Future<bool> _act(
    Future<DispatchDetail> Function(DispatchDetail run) action,
  ) async {
    final run = _state.valueOrNull;
    if (run == null || _busy) return false;
    _busy = true;
    _actionError = null;
    notifyListeners();
    try {
      await _show(await action(run));
      return true;
    } on ApiException catch (error) {
      _actionError = error;
      if (await _refreshAfterConflict(error)) _actionError = null;
      return false;
    } finally {
      _busy = false;
      _notify();
    }
  }

  // A 409 means the run moved under us. Returns true when the reload found the run ended, since the panel explains it better than the error.
  Future<bool> _refreshAfterConflict(ApiException error) async {
    if (!error.isConflict && !error.isNotFound) return false;
    try {
      final before = _ending;
      await _show(await _service.activeRun());
      return _ending != null && !identical(_ending, before);
    } on ApiException {
      return false;
    }
  }

  Future<void> _show(DispatchDetail? run) async {
    final live = run != null && (run.status?.isLive ?? false) ? run : null;
    final endedRunId = _liveRunId;
    if (endedRunId != null && endedRunId != live?.id) {
      final ending = await _endingOf(endedRunId, live?.id, knownEnd: run);
      if (ending != null) _ending = ending;
    }
    if (_liveRunId != live?.id) _handoverDraft = HandoverDraft.empty;
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
