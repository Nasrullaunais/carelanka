// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'roster_proposal_summary.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

RosterProposalSummary _$RosterProposalSummaryFromJson(
  Map<String, dynamic> json,
) => RosterProposalSummary(
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
);

Map<String, dynamic> _$RosterProposalSummaryToJson(
  RosterProposalSummary instance,
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
};
