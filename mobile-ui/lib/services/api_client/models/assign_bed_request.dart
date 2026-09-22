// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'assign_bed_request.g.dart';

@JsonSerializable()
class AssignBedRequest {
  const AssignBedRequest({
    required this.bedId,
    this.workflowId,
    this.overrideReason,
  });
  
  factory AssignBedRequest.fromJson(Map<String, Object?> json) => _$AssignBedRequestFromJson(json);
  
  @JsonKey(name: 'bed_id')
  final String bedId;
  @JsonKey(name: 'workflow_id')
  final String? workflowId;
  @JsonKey(name: 'override_reason')
  final String? overrideReason;

  Map<String, Object?> toJson() => _$AssignBedRequestToJson(this);
}
