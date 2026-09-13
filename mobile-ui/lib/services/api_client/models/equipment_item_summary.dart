// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'equipment_status.dart';

part 'equipment_item_summary.g.dart';

@JsonSerializable()
class EquipmentItemSummary {
  const EquipmentItemSummary({
    required this.id,
    required this.name,
    required this.categoryId,
    required this.categoryName,
    required this.model,
    required this.manufacturer,
    required this.assetTag,
    required this.status,
    this.wardId,
    this.wardName,
    this.nextMaintenanceDue,
  });
  
  factory EquipmentItemSummary.fromJson(Map<String, Object?> json) => _$EquipmentItemSummaryFromJson(json);
  
  final String id;
  final String name;
  @JsonKey(name: 'category_id')
  final String categoryId;
  @JsonKey(name: 'category_name')
  final String categoryName;
  final String model;
  final String manufacturer;
  @JsonKey(name: 'asset_tag')
  final String assetTag;
  @JsonKey(name: 'ward_id')
  final String? wardId;
  @JsonKey(name: 'ward_name')
  final String? wardName;
  final EquipmentStatus status;
  @JsonKey(name: 'next_maintenance_due')
  final DateTime? nextMaintenanceDue;

  Map<String, Object?> toJson() => _$EquipmentItemSummaryToJson(this);
}
