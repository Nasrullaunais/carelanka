// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'create_equipment_item_request.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

CreateEquipmentItemRequest _$CreateEquipmentItemRequestFromJson(
  Map<String, dynamic> json,
) => CreateEquipmentItemRequest(
  name: json['name'] as String,
  categoryId: json['category_id'] as String,
  model: json['model'] as String,
  manufacturer: json['manufacturer'] as String,
  purchaseDate: DateTime.parse(json['purchase_date'] as String),
  assetTag: json['asset_tag'] as String,
  serialNumber: json['serial_number'] as String?,
  wardId: json['ward_id'] as String?,
  nextMaintenanceDue: json['next_maintenance_due'] == null
      ? null
      : DateTime.parse(json['next_maintenance_due'] as String),
);

Map<String, dynamic> _$CreateEquipmentItemRequestToJson(
  CreateEquipmentItemRequest instance,
) => <String, dynamic>{
  'name': instance.name,
  'category_id': instance.categoryId,
  'model': instance.model,
  'manufacturer': instance.manufacturer,
  'purchase_date': instance.purchaseDate.toIso8601String(),
  'asset_tag': instance.assetTag,
  'serial_number': instance.serialNumber,
  'ward_id': instance.wardId,
  'next_maintenance_due': instance.nextMaintenanceDue?.toIso8601String(),
};
