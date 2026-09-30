import '../../../core/network/api.dart';
import '../../../core/network/api_exception.dart';
import '../../../services/api_client/care_lanka_api.dart';
import '../../../services/api_client/clients/my_run_api.dart';
import '../../../services/api_client/models/decline_dispatch_request.dart';
import '../../../services/api_client/models/dispatch_detail.dart';
import '../../../services/api_client/models/dispatch_status.dart';
import '../../../services/api_client/models/dispatch_summary_paged_result.dart';
import '../../../services/api_client/models/end_at_scene_request.dart';
import '../../../services/api_client/models/navigation_target.dart';
import '../../../services/api_client/models/record_handover_request.dart';
import '../../../services/api_client/models/scene_outcome.dart';
import '../../../services/api_client/models/update_my_dispatch_status_request.dart';
import 'crew_location_reporter.dart';

const historyPageSize = 20;

abstract interface class CrewRunService {
  Future<DispatchDetail?> activeRun();
  Future<DispatchDetail> getRun(String id);
  Future<DispatchDetail> acknowledge(String id);
  Future<DispatchDetail> decline(String id, String reason);
  Future<DispatchDetail> advance(
    String id,
    DispatchStatus next, {
    CrewPosition? position,
  });
  Future<DispatchDetail> handOver(
    String id, {
    String? notes,
    String? patientCondition,
  });
  Future<DispatchDetail> endAtScene(
    String id,
    SceneOutcome outcome, {
    String? notes,
  });
  Future<NavigationTarget> navigationTarget(String id);
  Future<DispatchSummaryPagedResult> history({required int page});
}

final class GeneratedCrewRunService implements CrewRunService {
  GeneratedCrewRunService(CareLankaApi api) : _run = api.myRun;

  final MyRunApi _run;

  @override
  Future<DispatchDetail?> activeRun() async {
    try {
      return await callApi(() => _run.getMyActiveDispatch());
    } on ApiException catch (error) {
      if (error.isNotFound) return null;
      rethrow;
    }
  }

  @override
  Future<DispatchDetail> getRun(String id) =>
      callApi(() => _run.getMyDispatch(id: id));

  @override
  Future<DispatchDetail> acknowledge(String id) =>
      callApi(() => _run.acknowledgeMyDispatch(id: id));

  @override
  Future<DispatchDetail> decline(String id, String reason) => callApi(
    () => _run.declineMyDispatch(
      id: id,
      body: DeclineDispatchRequest(reason: reason),
    ),
  );

  @override
  Future<DispatchDetail> advance(
    String id,
    DispatchStatus next, {
    CrewPosition? position,
  }) => callApi(
    () => _run.updateMyDispatchStatus(
      id: id,
      body: UpdateMyDispatchStatusRequest(
        status: next,
        latitude: position?.latitude,
        longitude: position?.longitude,
      ),
    ),
  );

  @override
  Future<DispatchDetail> handOver(
    String id, {
    String? notes,
    String? patientCondition,
  }) => callApi(
    () => _run.recordHandover(
      id: id,
      body: RecordHandoverRequest(
        notes: notes,
        patientCondition: patientCondition,
      ),
    ),
  );

  @override
  Future<DispatchDetail> endAtScene(
    String id,
    SceneOutcome outcome, {
    String? notes,
  }) => callApi(
    () => _run.endMyDispatchAtScene(
      id: id,
      body: EndAtSceneRequest(outcome: outcome, notes: notes),
    ),
  );

  @override
  Future<DispatchSummaryPagedResult> history({required int page}) => callApi(
    () => _run.getMyDispatchHistory(page: page, pageSize: historyPageSize),
  );

  @override
  Future<NavigationTarget> navigationTarget(String id) =>
      callApi(() => _run.getMyDispatchNavigationTarget(id: id));
}
