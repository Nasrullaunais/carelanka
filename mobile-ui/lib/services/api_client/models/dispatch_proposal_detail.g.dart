// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'dispatch_proposal_detail.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

DispatchProposalDetail _$DispatchProposalDetailFromJson(
  Map<String, dynamic> json,
) => DispatchProposalDetail(
  id: json['id'] as String?,
  workflowId: json['workflow_id'] as String?,
  emergencyCallId: json['emergency_call_id'] as String?,
  callPriority: json['call_priority'] == null
      ? null
      : CallPriority.fromJson(json['call_priority'] as String),
  status: json['status'] == null
      ? null
      : DispatchProposalStatus.fromJson(json['status'] as String),
  outcome: json['outcome'] == null
      ? null
      : DispatchOutcome.fromJson(json['outcome'] as String),
  isDiversion: json['is_diversion'] as bool?,
  proposedAmbulanceRegistration:
      json['proposed_ambulance_registration'] as String?,
  estimatedMinutesToScene: (json['estimated_minutes_to_scene'] as num?)
      ?.toInt(),
  createdAt: json['created_at'] == null
      ? null
      : DateTime.parse(json['created_at'] as String),
  objective: json['objective'] as String?,
  proposedAmbulanceId: json['proposed_ambulance_id'] as String?,
  proposedAmbulanceCurrentCrewCount:
      (json['proposed_ambulance_current_crew_count'] as num?)?.toInt(),
  proposedAmbulanceRequiredCrewCount:
      (json['proposed_ambulance_required_crew_count'] as num?)?.toInt(),
  rationale: json['rationale'] as String?,
  diversionImpact: json['diversion_impact'] == null
      ? null
      : DiversionImpact.fromJson(
          json['diversion_impact'] as Map<String, dynamic>,
        ),
  plan: (json['plan'] as List<dynamic>?)
      ?.map((e) => DispatchPlanStep.fromJson(e as Map<String, dynamic>))
      .toList(),
  validation: (json['validation'] as List<dynamic>?)
      ?.map((e) => DispatchValidationResult.fromJson(e as Map<String, dynamic>))
      .toList(),
  toolCalls: (json['tool_calls'] as List<dynamic>?)
      ?.map((e) => DispatchToolCall.fromJson(e as Map<String, dynamic>))
      .toList(),
  errors: (json['errors'] as List<dynamic>?)
      ?.map((e) => DispatchProposalError.fromJson(e as Map<String, dynamic>))
      .toList(),
  attemptCount: (json['attempt_count'] as num?)?.toInt(),
  startedAt: json['started_at'] == null
      ? null
      : DateTime.parse(json['started_at'] as String),
  completedAt: json['completed_at'] == null
      ? null
      : DateTime.parse(json['completed_at'] as String),
  resultingDispatchId: json['resulting_dispatch_id'] as String?,
  reviewedByStaffMemberId: json['reviewed_by_staff_member_id'] as String?,
  reviewedAt: json['reviewed_at'] == null
      ? null
      : DateTime.parse(json['reviewed_at'] as String),
  reviewNotes: json['review_notes'] as String?,
  rejectionReason: json['rejection_reason'] == null
      ? null
      : DispatchRejectionReason.fromJson(json['rejection_reason'] as String),
);

Map<String, dynamic> _$DispatchProposalDetailToJson(
  DispatchProposalDetail instance,
) => <String, dynamic>{
  'id': instance.id,
  'workflow_id': instance.workflowId,
  'emergency_call_id': instance.emergencyCallId,
  'call_priority': instance.callPriority,
  'status': instance.status,
  'outcome': instance.outcome,
  'is_diversion': instance.isDiversion,
  'proposed_ambulance_registration': instance.proposedAmbulanceRegistration,
  'estimated_minutes_to_scene': instance.estimatedMinutesToScene,
  'created_at': instance.createdAt?.toIso8601String(),
  'objective': instance.objective,
  'proposed_ambulance_id': instance.proposedAmbulanceId,
  'proposed_ambulance_current_crew_count':
      instance.proposedAmbulanceCurrentCrewCount,
  'proposed_ambulance_required_crew_count':
      instance.proposedAmbulanceRequiredCrewCount,
  'rationale': instance.rationale,
  'diversion_impact': instance.diversionImpact,
  'plan': instance.plan,
  'validation': instance.validation,
  'tool_calls': instance.toolCalls,
  'errors': instance.errors,
  'attempt_count': instance.attemptCount,
  'started_at': instance.startedAt?.toIso8601String(),
  'completed_at': instance.completedAt?.toIso8601String(),
  'resulting_dispatch_id': instance.resultingDispatchId,
  'reviewed_by_staff_member_id': instance.reviewedByStaffMemberId,
  'reviewed_at': instance.reviewedAt?.toIso8601String(),
  'review_notes': instance.reviewNotes,
  'rejection_reason': instance.rejectionReason,
};
