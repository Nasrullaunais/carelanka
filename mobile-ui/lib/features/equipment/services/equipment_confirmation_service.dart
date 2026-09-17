import '../../../core/network/api.dart';
import '../../../services/api_client/care_lanka_api.dart';
import '../../../services/api_client/models/equipment_item.dart';

class EquipmentConfirmationService {
  const EquipmentConfirmationService(this._api);

  final CareLankaApi _api;

  Future<int> countAwaiting() async {
    final result = await callApi(() => _api.equipment.countEquipmentItemsAwaitingConfirmation());
    return result.count;
  }

  Future<List<EquipmentItem>> listAwaiting(String code) {
    return callApi(() => _api.equipment.listEquipmentItemsAwaitingConfirmation(
          xConfirmationCode: code,
        ));
  }

  Future<EquipmentItem> confirm(String id, String code) {
    return callApi(() => _api.equipment.confirmEquipmentItem(id: id, xConfirmationCode: code));
  }

  Future<void> reject(String id, String code) {
    return callApi(() => _api.equipment.rejectEquipmentItem(id: id, xConfirmationCode: code));
  }
}
