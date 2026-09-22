// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'bed_workflow_status.dart';

part 'bed_workflow_accepted.g.dart';

@JsonSerializable()
class BedWorkflowAccepted {
  const BedWorkflowAccepted({
    this.workflowId,
    this.admissionId,
    this.status,
    this.pollUrl,
  });
  
  factory BedWorkflowAccepted.fromJson(Map<String, Object?> json) => _$BedWorkflowAcceptedFromJson(json);
  
  @JsonKey(name: 'workflow_id')
  final String? workflowId;
  @JsonKey(name: 'admission_id')
  final String? admissionId;
  final BedWorkflowStatus? status;
  @JsonKey(name: 'poll_url')
  final String? pollUrl;

  Map<String, Object?> toJson() => _$BedWorkflowAcceptedToJson(this);
}
