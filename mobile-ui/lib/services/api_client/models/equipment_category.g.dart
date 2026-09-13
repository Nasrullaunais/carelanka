// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'equipment_category.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

EquipmentCategory _$EquipmentCategoryFromJson(Map<String, dynamic> json) =>
    EquipmentCategory(
      id: json['id'] as String,
      name: json['name'] as String,
      createdAt: DateTime.parse(json['created_at'] as String),
      updatedAt: DateTime.parse(json['updated_at'] as String),
    );

Map<String, dynamic> _$EquipmentCategoryToJson(EquipmentCategory instance) =>
    <String, dynamic>{
      'id': instance.id,
      'name': instance.name,
      'created_at': instance.createdAt.toIso8601String(),
      'updated_at': instance.updatedAt.toIso8601String(),
    };
