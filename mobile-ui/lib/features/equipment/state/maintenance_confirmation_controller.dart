import '../../../services/api_client/models/maintenance_schedule.dart';
import '../services/maintenance_confirmation_service.dart';
import 'confirmation_queue_controller.dart';

class MaintenanceConfirmationController extends ConfirmationQueueController<MaintenanceSchedule> {
  MaintenanceConfirmationController(this._service);

  final MaintenanceConfirmationService _service;

  @override
  String idOf(MaintenanceSchedule job) => job.id;

  @override
  Future<int> fetchCount() => _service.countAwaiting();

  @override
  Future<List<MaintenanceSchedule>> fetchAwaiting(String code) => _service.listAwaiting(code);

  @override
  Future<void> sendConfirm(MaintenanceSchedule job, String code) => _service.confirm(job.id, code);
}
