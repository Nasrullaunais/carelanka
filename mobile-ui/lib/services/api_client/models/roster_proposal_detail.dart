// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'agent_outcome.dart';
import 'plan_step_dto.dart';
import 'rejection_reason.dart';
import 'roster_proposal_error_dto.dart';
import 'roster_proposal_status.dart';
import 'roster_proposed_change_dto.dart';
import 'roster_validation_result.dart';
import 'tool_call_dto.dart';

part 'roster_proposal_detail.g.dart';

@JsonSerializable()
class RosterProposalDetail {
  const RosterProposalDetail({
    this.id,
    this.workflowId,
    this.shiftId,
    this.wardName,
    this.shiftDate,
    this.objective,
    this.status,
    this.outcome,
    this.isCascadingSwap,
    this.changeCount,
    this.createdAt,
    this.plan,
    this.proposedChanges,
    this.validation,
    this.toolCalls,
    this.errors,
    this.attemptCount,
    this.startedAt,
    this.completedAt,
    this.reviewedByStaffId,
    this.reviewedByStaffMemberId,
    this.reviewedAt,
    this.reviewNotes,
    this.rejectionReason,
    this.finalOutcome,
  });
  
  factory RosterProposalDetail.fromJson(Map<String, Object?> json) => _$RosterProposalDetailFromJson(json);
  
  final String? id;
  @JsonKey(name: 'workflow_id')
  final String? workflowId;
  @JsonKey(name: 'shift_id')
  final String? shiftId;
  @JsonKey(name: 'ward_name')
  final String? wardName;
  @JsonKey(name: 'shift_date')
  final DateTime? shiftDate;
  final String? objective;
  final RosterProposalStatus? status;
  final AgentOutcome? outcome;
  @JsonKey(name: 'is_cascading_swap')
  final bool? isCascadingSwap;
  @JsonKey(name: 'change_count')
  final int? changeCount;
  @JsonKey(name: 'created_at')
  final DateTime? createdAt;
  final List<PlanStepDto>? plan;
  @JsonKey(name: 'proposed_changes')
  final List<RosterProposedChangeDto>? proposedChanges;
  final List<RosterValidationResult>? validation;
  @JsonKey(name: 'tool_calls')
  final List<ToolCallDto>? toolCalls;
  final List<RosterProposalErrorDto>? errors;
  @JsonKey(name: 'attempt_count')
  final int? attemptCount;
  @JsonKey(name: 'started_at')
  final DateTime? startedAt;
  @JsonKey(name: 'completed_at')
  final DateTime? completedAt;
  @JsonKey(name: 'reviewed_by_staff_id')
  final String? reviewedByStaffId;
  @JsonKey(name: 'reviewed_by_staff_member_id')
  final String? reviewedByStaffMemberId;
  @JsonKey(name: 'reviewed_at')
  final DateTime? reviewedAt;
  @JsonKey(name: 'review_notes')
  final String? reviewNotes;
  @JsonKey(name: 'rejection_reason')
  final RejectionReason? rejectionReason;
  @JsonKey(name: 'final_outcome')
  final String? finalOutcome;

  Map<String, Object?> toJson() => _$RosterProposalDetailToJson(this);
}
