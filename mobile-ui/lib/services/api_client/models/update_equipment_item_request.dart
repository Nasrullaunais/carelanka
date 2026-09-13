// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'equipment_status.dart';

part 'update_equipment_item_request.g.dart';

@JsonSerializable()
class UpdateEquipmentItemRequest {
  const UpdateEquipmentItemRequest({
    this.name,
    this.model,
    this.manufacturer,
    this.status,
    this.wardId,
    this.nextMaintenanceDue,
  });
  
  factory UpdateEquipmentItemRequest.fromJson(Map<String, Object?> json) => _$UpdateEquipmentItemRequestFromJson(json);
  
  final String? name;
  final String? model;
  final String? manufacturer;
  final EquipmentStatus? status;
  @JsonKey(name: 'ward_id')
  final String? wardId;
  @JsonKey(name: 'next_maintenance_due')
  final DateTime? nextMaintenanceDue;

  Map<String, Object?> toJson() => _$UpdateEquipmentItemRequestToJson(this);
}
