import 'dart:async';

import 'package:flutter/foundation.dart';

import '../../../core/network/api_exception.dart';
import '../../../core/widgets/async_data.dart';
import '../../../services/api_client/models/bed_assignment.dart';
import '../../../services/api_client/models/bed_workflow_status.dart';
import '../../../services/api_client/models/bed_workflow_summary.dart';
import '../services/patient_service.dart';

const _pollInterval = Duration(milliseconds: 1500);

/// Runs the bed agent and polls the workflow until it pauses for a decision. Confirming any
/// bed — the suggested one or an alternative — goes through the same `assign-bed` endpoint a
/// manual pick uses; there is no separate approval step (§8.6b).
class BedSuggestionController extends ChangeNotifier {
  BedSuggestionController(this._service);

  final PatientService _service;
  Timer? _poll;

  AsyncData<BedWorkflowSummary>? _workflow;
  bool _starting = false;
  ApiException? _startError;
  bool _assigning = false;
  ApiException? _assignError;
  BedAssignment? _assigned;

  AsyncData<BedWorkflowSummary>? get workflow => _workflow;
  bool get starting => _starting;
  ApiException? get startError => _startError;
  bool get assigning => _assigning;
  ApiException? get assignError => _assignError;
  BedAssignment? get assigned => _assigned;

  Future<void> start({String? admissionId, String? patientIdentifier}) async {
    _starting = true;
    _startError = null;
    _workflow = null;
    notifyListeners();

    try {
      final accepted = await _service.requestBedSuggestion(
        admissionId: admissionId,
        patientIdentifier: patientIdentifier,
      );

      final workflowId = accepted.workflowId;
      if (workflowId == null) {
        throw const ApiException(message: 'The agent did not start a run.');
      }

      _workflow = const AsyncData.loading();
      _starting = false;
      notifyListeners();

      await _refresh(workflowId);
      _poll = Timer.periodic(_pollInterval, (_) => _refresh(workflowId));
    } on ApiException catch (error) {
      _startError = error;
      _starting = false;
      notifyListeners();
    }
  }

  Future<void> _refresh(String workflowId) async {
    try {
      final summary = await _service.getBedWorkflow(workflowId);
      _workflow = AsyncData.ready(summary);

      if (summary.status != BedWorkflowStatus.running) {
        _poll?.cancel();
      }
    } on ApiException catch (error) {
      _workflow = AsyncData.failed(error);
      _poll?.cancel();
    }
    notifyListeners();
  }

  /// Returns true once the bed is held, so the screen can close itself only on success.
  Future<bool> assign({
    required String admissionId,
    required String bedId,
    required String workflowId,
    String? overrideReason,
  }) async {
    _assigning = true;
    _assignError = null;
    notifyListeners();

    try {
      _assigned = await _service.assignBed(
        admissionId: admissionId,
        bedId: bedId,
        workflowId: workflowId,
        overrideReason: overrideReason,
      );
      return true;
    } on ApiException catch (error) {
      _assignError = error;
      return false;
    } finally {
      _assigning = false;
      notifyListeners();
    }
  }

  @override
  void dispose() {
    _poll?.cancel();
    super.dispose();
  }
}
