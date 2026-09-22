import '../../../core/network/api.dart';
import '../../../services/api_client/care_lanka_api.dart';
import '../../../services/api_client/models/maintenance_schedule.dart';

class MaintenanceConfirmationService {
  const MaintenanceConfirmationService(this._api);

  final CareLankaApi _api;

  Future<int> countAwaiting() async {
    final result =
        await callApi(() => _api.maintenance.countMaintenanceSchedulesAwaitingConfirmation());
    return result.count;
  }

  Future<List<MaintenanceSchedule>> listAwaiting(String code) {
    return callApi(() => _api.maintenance.listMaintenanceSchedulesAwaitingConfirmation(
          xConfirmationCode: code,
        ));
  }

  Future<MaintenanceSchedule> confirm(String id, String code) {
    return callApi(
        () => _api.maintenance.confirmMaintenanceSchedule(id: id, xConfirmationCode: code));
  }
}
