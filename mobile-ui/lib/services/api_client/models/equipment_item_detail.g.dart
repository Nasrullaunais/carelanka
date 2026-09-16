// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'equipment_item_detail.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

EquipmentItemDetail _$EquipmentItemDetailFromJson(Map<String, dynamic> json) =>
    EquipmentItemDetail(
      id: json['id'] as String,
      name: json['name'] as String,
      categoryId: json['category_id'] as String,
      categoryName: json['category_name'] as String,
      model: json['model'] as String,
      manufacturer: json['manufacturer'] as String,
      assetTag: json['asset_tag'] as String,
      status: EquipmentStatus.fromJson(json['status'] as String),
      purchaseDate: DateTime.parse(json['purchase_date'] as String),
      awaitingConfirmation: json['awaiting_confirmation'] as bool,
      createdAt: DateTime.parse(json['created_at'] as String),
      updatedAt: DateTime.parse(json['updated_at'] as String),
      maintenanceHistory: (json['maintenance_history'] as List<dynamic>)
          .map((e) => MaintenanceSchedule.fromJson(e as Map<String, dynamic>))
          .toList(),
      openWarnings: (json['open_warnings'] as List<dynamic>)
          .map((e) => Warning.fromJson(e as Map<String, dynamic>))
          .toList(),
      wardId: json['ward_id'] as String?,
      wardName: json['ward_name'] as String?,
      nextMaintenanceDue: json['next_maintenance_due'] == null
          ? null
          : DateTime.parse(json['next_maintenance_due'] as String),
      serialNumber: json['serial_number'] as String?,
      assignedToAdmissionId: json['assigned_to_admission_id'] as String?,
      confirmedByStaffId: json['confirmed_by_staff_id'] as String?,
      confirmedAt: json['confirmed_at'] == null
          ? null
          : DateTime.parse(json['confirmed_at'] as String),
    );

Map<String, dynamic> _$EquipmentItemDetailToJson(
  EquipmentItemDetail instance,
) => <String, dynamic>{
  'id': instance.id,
  'name': instance.name,
  'category_id': instance.categoryId,
  'category_name': instance.categoryName,
  'model': instance.model,
  'manufacturer': instance.manufacturer,
  'asset_tag': instance.assetTag,
  'ward_id': instance.wardId,
  'ward_name': instance.wardName,
  'status': instance.status,
  'next_maintenance_due': instance.nextMaintenanceDue?.toIso8601String(),
  'purchase_date': instance.purchaseDate.toIso8601String(),
  'serial_number': instance.serialNumber,
  'assigned_to_admission_id': instance.assignedToAdmissionId,
  'awaiting_confirmation': instance.awaitingConfirmation,
  'confirmed_by_staff_id': instance.confirmedByStaffId,
  'confirmed_at': instance.confirmedAt?.toIso8601String(),
  'created_at': instance.createdAt.toIso8601String(),
  'updated_at': instance.updatedAt.toIso8601String(),
  'maintenance_history': instance.maintenanceHistory,
  'open_warnings': instance.openWarnings,
};
