// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'pending_equipment_count.g.dart';

@JsonSerializable()
class PendingEquipmentCount {
  const PendingEquipmentCount({
    required this.count,
  });
  
  factory PendingEquipmentCount.fromJson(Map<String, Object?> json) => _$PendingEquipmentCountFromJson(json);
  
  final int count;

  Map<String, Object?> toJson() => _$PendingEquipmentCountToJson(this);
}
