// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'equipment_category.g.dart';

@JsonSerializable()
class EquipmentCategory {
  const EquipmentCategory({
    required this.id,
    required this.name,
    required this.createdAt,
    required this.updatedAt,
  });
  
  factory EquipmentCategory.fromJson(Map<String, Object?> json) => _$EquipmentCategoryFromJson(json);
  
  final String id;
  final String name;
  @JsonKey(name: 'created_at')
  final DateTime createdAt;
  @JsonKey(name: 'updated_at')
  final DateTime updatedAt;

  Map<String, Object?> toJson() => _$EquipmentCategoryToJson(this);
}
