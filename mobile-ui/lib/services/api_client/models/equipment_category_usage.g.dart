// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'equipment_category_usage.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

EquipmentCategoryUsage _$EquipmentCategoryUsageFromJson(
  Map<String, dynamic> json,
) => EquipmentCategoryUsage(
  id: json['id'] as String,
  name: json['name'] as String,
  itemCount: (json['item_count'] as num).toInt(),
);

Map<String, dynamic> _$EquipmentCategoryUsageToJson(
  EquipmentCategoryUsage instance,
) => <String, dynamic>{
  'id': instance.id,
  'name': instance.name,
  'item_count': instance.itemCount,
};
