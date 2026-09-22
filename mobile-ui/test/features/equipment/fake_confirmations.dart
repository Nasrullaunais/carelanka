import 'package:carelanka_mobile/core/network/api_exception.dart';
import 'package:carelanka_mobile/features/equipment/services/equipment_confirmation_service.dart';
import 'package:carelanka_mobile/features/equipment/services/maintenance_confirmation_service.dart';
import 'package:carelanka_mobile/services/api_client/models/asset_type.dart';
import 'package:carelanka_mobile/services/api_client/models/equipment_item.dart';
import 'package:carelanka_mobile/services/api_client/models/equipment_status.dart';
import 'package:carelanka_mobile/services/api_client/models/maintenance_schedule.dart';
import 'package:carelanka_mobile/services/api_client/models/maintenance_status.dart';
import 'package:carelanka_mobile/services/api_client/models/maintenance_type.dart';
import 'package:carelanka_mobile/services/api_client/models/raised_by.dart';

const confirmationCode = 'equipment2026';

const wrongCode = ApiException(
  message: 'That confirmation code is not correct.',
  statusCode: 403,
  code: 'cl_equ_017',
);

class FakeEquipmentConfirmationService implements EquipmentConfirmationService {
  FakeEquipmentConfirmationService({List<EquipmentItem>? items, this.actionFailure})
    : items = items ?? [];

  List<EquipmentItem> items;
  ApiException? actionFailure;

  final codesSent = <String>[];
  final confirmed = <String>[];
  final rejected = <String>[];

  void _check(String code) {
    codesSent.add(code);
    if (code != confirmationCode) throw wrongCode;
  }

  @override
  Future<int> countAwaiting() async => items.length;

  @override
  Future<List<EquipmentItem>> listAwaiting(String code) async {
    _check(code);
    return List.of(items);
  }

  @override
  Future<EquipmentItem> confirm(String id, String code) async {
    _check(code);
    final failure = actionFailure;
    if (failure != null) throw failure;

    confirmed.add(id);
    final item = items.firstWhere((i) => i.id == id);
    items.remove(item);
    return item;
  }

  @override
  Future<void> reject(String id, String code) async {
    _check(code);
    final failure = actionFailure;
    if (failure != null) throw failure;

    rejected.add(id);
    items.removeWhere((i) => i.id == id);
  }
}

EquipmentItem pendingItem({
  String id = 'item-1',
  String name = 'Ventilator',
  String tag = 'EQ-0101',
}) => EquipmentItem(
  id: id,
  name: name,
  categoryId: 'category-1',
  categoryName: 'Life support',
  model: 'V-100',
  manufacturer: 'Acme Medical',
  assetTag: tag,
  status: EquipmentStatus.available,
  purchaseDate: DateTime(2026, 9, 1),
  awaitingConfirmation: true,
  createdAt: DateTime.utc(2026, 9, 16, 4, 30),
  updatedAt: DateTime.utc(2026, 9, 16, 4, 30),
);

class FakeMaintenanceConfirmationService implements MaintenanceConfirmationService {
  FakeMaintenanceConfirmationService({List<MaintenanceSchedule>? jobs}) : jobs = jobs ?? [];

  List<MaintenanceSchedule> jobs;

  final confirmed = <String>[];

  void _check(String code) {
    if (code != confirmationCode) {
      throw const ApiException(
        message: 'That confirmation code is not correct.',
        statusCode: 403,
        code: 'cl_equ_017',
      );
    }
  }

  @override
  Future<int> countAwaiting() async => jobs.length;

  @override
  Future<List<MaintenanceSchedule>> listAwaiting(String code) async {
    _check(code);
    return List.of(jobs);
  }

  @override
  Future<MaintenanceSchedule> confirm(String id, String code) async {
    _check(code);
    confirmed.add(id);
    final job = jobs.firstWhere((j) => j.id == id);
    jobs.remove(job);
    return job;
  }
}

MaintenanceSchedule openJob({
  String id = 'job-1',
  String label = 'Ventilator (asset tag EQ-0101)',
}) => MaintenanceSchedule(
  id: id,
  assetType: AssetType.equipmentItem,
  assetId: 'item-1',
  assetLabel: label,
  scheduleType: MaintenanceType.routineService,
  scheduledDate: DateTime(2026, 9, 16),
  status: MaintenanceStatus.scheduled,
  notes: 'Alarm will not silence.',
  createdBy: RaisedBy.user,
  createdAt: DateTime.utc(2026, 9, 10, 4, 30),
  updatedAt: DateTime.utc(2026, 9, 16, 6, 0),
);
