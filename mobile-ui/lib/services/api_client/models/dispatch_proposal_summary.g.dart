// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'dispatch_proposal_summary.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

DispatchProposalSummary _$DispatchProposalSummaryFromJson(
  Map<String, dynamic> json,
) => DispatchProposalSummary(
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
);

Map<String, dynamic> _$DispatchProposalSummaryToJson(
  DispatchProposalSummary instance,
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
};
