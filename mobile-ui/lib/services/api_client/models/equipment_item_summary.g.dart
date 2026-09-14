// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'equipment_item_summary.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

EquipmentItemSummary _$EquipmentItemSummaryFromJson(
  Map<String, dynamic> json,
) => EquipmentItemSummary(
  id: json['id'] as String,
  name: json['name'] as String,
  categoryId: json['category_id'] as String,
  categoryName: json['category_name'] as String,
  model: json['model'] as String,
  manufacturer: json['manufacturer'] as String,
  assetTag: json['asset_tag'] as String,
  status: EquipmentStatus.fromJson(json['status'] as String),
  wardId: json['ward_id'] as String?,
  wardName: json['ward_name'] as String?,
  nextMaintenanceDue: json['next_maintenance_due'] == null
      ? null
      : DateTime.parse(json['next_maintenance_due'] as String),
);

Map<String, dynamic> _$EquipmentItemSummaryToJson(
  EquipmentItemSummary instance,
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
};
