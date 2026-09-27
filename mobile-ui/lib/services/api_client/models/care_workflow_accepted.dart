// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'care_workflow_accepted.g.dart';

@JsonSerializable()
class CareWorkflowAccepted {
  const CareWorkflowAccepted({
    this.workflowId,
    this.recommendationId,
    this.status,
    this.pollUrl,
    this.redFlag,
  });
  
  factory CareWorkflowAccepted.fromJson(Map<String, Object?> json) => _$CareWorkflowAcceptedFromJson(json);
  
  @JsonKey(name: 'workflow_id')
  final String? workflowId;
  @JsonKey(name: 'recommendation_id')
  final String? recommendationId;
  final String? status;
  @JsonKey(name: 'poll_url')
  final String? pollUrl;
  @JsonKey(name: 'red_flag')
  final bool? redFlag;

  Map<String, Object?> toJson() => _$CareWorkflowAcceptedToJson(this);
}
