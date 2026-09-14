// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'update_equipment_item_request.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

UpdateEquipmentItemRequest _$UpdateEquipmentItemRequestFromJson(
  Map<String, dynamic> json,
) => UpdateEquipmentItemRequest(
  name: json['name'] as String?,
  model: json['model'] as String?,
  manufacturer: json['manufacturer'] as String?,
  status: json['status'] == null
      ? null
      : EquipmentStatus.fromJson(json['status'] as String),
  wardId: json['ward_id'] as String?,
  nextMaintenanceDue: json['next_maintenance_due'] == null
      ? null
      : DateTime.parse(json['next_maintenance_due'] as String),
);

Map<String, dynamic> _$UpdateEquipmentItemRequestToJson(
  UpdateEquipmentItemRequest instance,
) => <String, dynamic>{
  'name': instance.name,
  'model': instance.model,
  'manufacturer': instance.manufacturer,
  'status': instance.status,
  'ward_id': instance.wardId,
  'next_maintenance_due': instance.nextMaintenanceDue?.toIso8601String(),
};
