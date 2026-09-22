import '../../../core/network/api_exception.dart';
import '../../../services/api_client/models/equipment_item.dart';
import '../services/equipment_confirmation_service.dart';
import 'confirmation_queue_controller.dart';

class EquipmentConfirmationController extends ConfirmationQueueController<EquipmentItem> {
  EquipmentConfirmationController(this._service);

  final EquipmentConfirmationService _service;

  @override
  String idOf(EquipmentItem item) => item.id;

  @override
  Future<int> fetchCount() => _service.countAwaiting();

  @override
  Future<List<EquipmentItem>> fetchAwaiting(String code) => _service.listAwaiting(code);

  @override
  Future<void> sendConfirm(EquipmentItem item, String code) => _service.confirm(item.id, code);

  /// Returns the server's refusal, or null when the item was rejected.
  Future<ApiException?> reject(EquipmentItem item) =>
      act(item, (item, code) => _service.reject(item.id, code));
}
