// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'bed_agent_outcome.dart';
import 'bed_agent_step.dart';
import 'bed_approver_role.dart';
import 'bed_suggestion_blocker.dart';
import 'bed_suggestion_patient.dart';
import 'bed_workflow_status.dart';
import 'bed_workflow_validation.dart';
import 'suggested_bed.dart';

part 'bed_workflow_summary.g.dart';

@JsonSerializable()
class BedWorkflowSummary {
  const BedWorkflowSummary({
    this.workflowId,
    this.admissionId,
    this.objective,
    this.status,
    this.outcome,
    this.plan,
    this.steps,
    this.patient,
    this.best,
    this.alternatives,
    this.blocker,
    this.validation,
    this.requiresApprovalBy,
    this.retries,
  });
  
  factory BedWorkflowSummary.fromJson(Map<String, Object?> json) => _$BedWorkflowSummaryFromJson(json);
  
  @JsonKey(name: 'workflow_id')
  final String? workflowId;
  @JsonKey(name: 'admission_id')
  final String? admissionId;
  final String? objective;
  final BedWorkflowStatus? status;
  final BedAgentOutcome? outcome;
  final List<String>? plan;
  final List<BedAgentStep>? steps;
  final BedSuggestionPatient? patient;
  final SuggestedBed? best;
  final List<SuggestedBed>? alternatives;
  final BedSuggestionBlocker? blocker;
  final BedWorkflowValidation? validation;
  @JsonKey(name: 'requires_approval_by')
  final BedApproverRole? requiresApprovalBy;
  final int? retries;

  Map<String, Object?> toJson() => _$BedWorkflowSummaryToJson(this);
}
