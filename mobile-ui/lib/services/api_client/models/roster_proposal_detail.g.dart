// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'roster_proposal_detail.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

RosterProposalDetail _$RosterProposalDetailFromJson(
  Map<String, dynamic> json,
) => RosterProposalDetail(
  id: json['id'] as String?,
  workflowId: json['workflow_id'] as String?,
  shiftId: json['shift_id'] as String?,
  wardName: json['ward_name'] as String?,
  shiftDate: json['shift_date'] == null
      ? null
      : DateTime.parse(json['shift_date'] as String),
  objective: json['objective'] as String?,
  status: json['status'] == null
      ? null
      : RosterProposalStatus.fromJson(json['status'] as String),
  outcome: json['outcome'] == null
      ? null
      : AgentOutcome.fromJson(json['outcome'] as String),
  isCascadingSwap: json['is_cascading_swap'] as bool?,
  changeCount: (json['change_count'] as num?)?.toInt(),
  createdAt: json['created_at'] == null
      ? null
      : DateTime.parse(json['created_at'] as String),
  plan: (json['plan'] as List<dynamic>?)
      ?.map((e) => PlanStepDto.fromJson(e as Map<String, dynamic>))
      .toList(),
  proposedChanges: (json['proposed_changes'] as List<dynamic>?)
      ?.map((e) => RosterProposedChangeDto.fromJson(e as Map<String, dynamic>))
      .toList(),
  validation: (json['validation'] as List<dynamic>?)
      ?.map((e) => RosterValidationResult.fromJson(e as Map<String, dynamic>))
      .toList(),
  toolCalls: (json['tool_calls'] as List<dynamic>?)
      ?.map((e) => ToolCallDto.fromJson(e as Map<String, dynamic>))
      .toList(),
  errors: (json['errors'] as List<dynamic>?)
      ?.map((e) => RosterProposalErrorDto.fromJson(e as Map<String, dynamic>))
      .toList(),
  attemptCount: (json['attempt_count'] as num?)?.toInt(),
  startedAt: json['started_at'] == null
      ? null
      : DateTime.parse(json['started_at'] as String),
  completedAt: json['completed_at'] == null
      ? null
      : DateTime.parse(json['completed_at'] as String),
  reviewedByStaffId: json['reviewed_by_staff_id'] as String?,
  reviewedByStaffMemberId: json['reviewed_by_staff_member_id'] as String?,
  reviewedAt: json['reviewed_at'] == null
      ? null
      : DateTime.parse(json['reviewed_at'] as String),
  reviewNotes: json['review_notes'] as String?,
  rejectionReason: json['rejection_reason'] == null
      ? null
      : RejectionReason.fromJson(json['rejection_reason'] as String),
  finalOutcome: json['final_outcome'] as String?,
);

Map<String, dynamic> _$RosterProposalDetailToJson(
  RosterProposalDetail instance,
) => <String, dynamic>{
  'id': instance.id,
  'workflow_id': instance.workflowId,
  'shift_id': instance.shiftId,
  'ward_name': instance.wardName,
  'shift_date': instance.shiftDate?.toIso8601String(),
  'objective': instance.objective,
  'status': instance.status,
  'outcome': instance.outcome,
  'is_cascading_swap': instance.isCascadingSwap,
  'change_count': instance.changeCount,
  'created_at': instance.createdAt?.toIso8601String(),
  'plan': instance.plan,
  'proposed_changes': instance.proposedChanges,
  'validation': instance.validation,
  'tool_calls': instance.toolCalls,
  'errors': instance.errors,
  'attempt_count': instance.attemptCount,
  'started_at': instance.startedAt?.toIso8601String(),
  'completed_at': instance.completedAt?.toIso8601String(),
  'reviewed_by_staff_id': instance.reviewedByStaffId,
  'reviewed_by_staff_member_id': instance.reviewedByStaffMemberId,
  'reviewed_at': instance.reviewedAt?.toIso8601String(),
  'review_notes': instance.reviewNotes,
  'rejection_reason': instance.rejectionReason,
  'final_outcome': instance.finalOutcome,
};
