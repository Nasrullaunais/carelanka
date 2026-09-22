// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'equipment_category_usage.g.dart';

@JsonSerializable()
class EquipmentCategoryUsage {
  const EquipmentCategoryUsage({
    required this.id,
    required this.name,
    required this.itemCount,
  });
  
  factory EquipmentCategoryUsage.fromJson(Map<String, Object?> json) => _$EquipmentCategoryUsageFromJson(json);
  
  final String id;
  final String name;
  @JsonKey(name: 'item_count')
  final int itemCount;

  Map<String, Object?> toJson() => _$EquipmentCategoryUsageToJson(this);
}
