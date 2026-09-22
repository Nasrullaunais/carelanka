// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'bed_agent_step.dart';
import 'care_agent_outcome.dart';
import 'care_draft_source.dart';
import 'care_workflow_status.dart';
import 'care_workflow_validation.dart';

part 'care_workflow_summary.g.dart';

@JsonSerializable()
class CareWorkflowSummary {
  const CareWorkflowSummary({
    this.workflowId,
    this.recommendationId,
    this.objective,
    this.status,
    this.outcome,
    this.plan,
    this.steps,
    this.redFlag,
    this.validation,
    this.draftSource,
    this.draftNote,
    this.retries,
  });
  
  factory CareWorkflowSummary.fromJson(Map<String, Object?> json) => _$CareWorkflowSummaryFromJson(json);
  
  @JsonKey(name: 'workflow_id')
  final String? workflowId;
  @JsonKey(name: 'recommendation_id')
  final String? recommendationId;
  final String? objective;
  final CareWorkflowStatus? status;
  final CareAgentOutcome? outcome;
  final List<String>? plan;
  final List<BedAgentStep>? steps;
  @JsonKey(name: 'red_flag')
  final bool? redFlag;
  final CareWorkflowValidation? validation;
  @JsonKey(name: 'draft_source')
  final CareDraftSource? draftSource;
  @JsonKey(name: 'draft_note')
  final String? draftNote;
  final int? retries;

  Map<String, Object?> toJson() => _$CareWorkflowSummaryToJson(this);
}
