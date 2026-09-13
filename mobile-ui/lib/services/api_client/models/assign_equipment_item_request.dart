// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'assign_equipment_item_request.g.dart';

@JsonSerializable()
class AssignEquipmentItemRequest {
  const AssignEquipmentItemRequest({
    required this.admissionId,
  });
  
  factory AssignEquipmentItemRequest.fromJson(Map<String, Object?> json) => _$AssignEquipmentItemRequestFromJson(json);
  
  @JsonKey(name: 'admission_id')
  final String admissionId;

  Map<String, Object?> toJson() => _$AssignEquipmentItemRequestToJson(this);
}
