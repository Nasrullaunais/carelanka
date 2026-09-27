// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'call_priority.dart';
import 'dispatch_outcome.dart';
import 'dispatch_plan_step.dart';
import 'dispatch_proposal_error.dart';
import 'dispatch_proposal_status.dart';
import 'dispatch_rejection_reason.dart';
import 'dispatch_tool_call.dart';
import 'dispatch_validation_result.dart';
import 'diversion_impact.dart';

part 'dispatch_proposal_detail.g.dart';

@JsonSerializable()
class DispatchProposalDetail {
  const DispatchProposalDetail({
    this.id,
    this.workflowId,
    this.emergencyCallId,
    this.callPriority,
    this.status,
    this.outcome,
    this.isDiversion,
    this.proposedAmbulanceRegistration,
    this.estimatedMinutesToScene,
    this.createdAt,
    this.objective,
    this.proposedAmbulanceId,
    this.proposedAmbulanceCurrentCrewCount,
    this.proposedAmbulanceRequiredCrewCount,
    this.rationale,
    this.diversionImpact,
    this.plan,
    this.validation,
    this.toolCalls,
    this.errors,
    this.attemptCount,
    this.startedAt,
    this.completedAt,
    this.resultingDispatchId,
    this.reviewedByStaffMemberId,
    this.reviewedAt,
    this.reviewNotes,
    this.rejectionReason,
  });
  
  factory DispatchProposalDetail.fromJson(Map<String, Object?> json) => _$DispatchProposalDetailFromJson(json);
  
  final String? id;
  @JsonKey(name: 'workflow_id')
  final String? workflowId;
  @JsonKey(name: 'emergency_call_id')
  final String? emergencyCallId;
  @JsonKey(name: 'call_priority')
  final CallPriority? callPriority;
  final DispatchProposalStatus? status;
  final DispatchOutcome? outcome;
  @JsonKey(name: 'is_diversion')
  final bool? isDiversion;
  @JsonKey(name: 'proposed_ambulance_registration')
  final String? proposedAmbulanceRegistration;
  @JsonKey(name: 'estimated_minutes_to_scene')
  final int? estimatedMinutesToScene;
  @JsonKey(name: 'created_at')
  final DateTime? createdAt;
  final String? objective;
  @JsonKey(name: 'proposed_ambulance_id')
  final String? proposedAmbulanceId;
  @JsonKey(name: 'proposed_ambulance_current_crew_count')
  final int? proposedAmbulanceCurrentCrewCount;
  @JsonKey(name: 'proposed_ambulance_required_crew_count')
  final int? proposedAmbulanceRequiredCrewCount;
  final String? rationale;
  @JsonKey(name: 'diversion_impact')
  final DiversionImpact? diversionImpact;
  final List<DispatchPlanStep>? plan;
  final List<DispatchValidationResult>? validation;
  @JsonKey(name: 'tool_calls')
  final List<DispatchToolCall>? toolCalls;
  final List<DispatchProposalError>? errors;
  @JsonKey(name: 'attempt_count')
  final int? attemptCount;
  @JsonKey(name: 'started_at')
  final DateTime? startedAt;
  @JsonKey(name: 'completed_at')
  final DateTime? completedAt;
  @JsonKey(name: 'resulting_dispatch_id')
  final String? resultingDispatchId;
  @JsonKey(name: 'reviewed_by_staff_member_id')
  final String? reviewedByStaffMemberId;
  @JsonKey(name: 'reviewed_at')
  final DateTime? reviewedAt;
  @JsonKey(name: 'review_notes')
  final String? reviewNotes;
  @JsonKey(name: 'rejection_reason')
  final DispatchRejectionReason? rejectionReason;

  Map<String, Object?> toJson() => _$DispatchProposalDetailToJson(this);
}
