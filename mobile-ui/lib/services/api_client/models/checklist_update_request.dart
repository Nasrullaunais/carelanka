// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'checklist_update_request.g.dart';

@JsonSerializable()
class ChecklistUpdateRequest {
  const ChecklistUpdateRequest({
    this.clinicalClearance,
    this.billingSettled,
  });
  
  factory ChecklistUpdateRequest.fromJson(Map<String, Object?> json) => _$ChecklistUpdateRequestFromJson(json);
  
  @JsonKey(name: 'clinical_clearance')
  final bool? clinicalClearance;
  @JsonKey(name: 'billing_settled')
  final bool? billingSettled;

  Map<String, Object?> toJson() => _$ChecklistUpdateRequestToJson(this);
}
