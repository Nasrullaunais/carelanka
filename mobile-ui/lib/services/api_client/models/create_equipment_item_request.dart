// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'create_equipment_item_request.g.dart';

@JsonSerializable()
class CreateEquipmentItemRequest {
  const CreateEquipmentItemRequest({
    required this.name,
    required this.categoryId,
    required this.model,
    required this.manufacturer,
    required this.purchaseDate,
    required this.assetTag,
    this.serialNumber,
    this.wardId,
    this.nextMaintenanceDue,
  });
  
  factory CreateEquipmentItemRequest.fromJson(Map<String, Object?> json) => _$CreateEquipmentItemRequestFromJson(json);
  
  final String name;
  @JsonKey(name: 'category_id')
  final String categoryId;
  final String model;
  final String manufacturer;
  @JsonKey(name: 'purchase_date')
  final DateTime purchaseDate;
  @JsonKey(name: 'asset_tag')
  final String assetTag;
  @JsonKey(name: 'serial_number')
  final String? serialNumber;
  @JsonKey(name: 'ward_id')
  final String? wardId;
  @JsonKey(name: 'next_maintenance_due')
  final DateTime? nextMaintenanceDue;

  Map<String, Object?> toJson() => _$CreateEquipmentItemRequestToJson(this);
}
