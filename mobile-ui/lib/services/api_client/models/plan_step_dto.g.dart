// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'plan_step_dto.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

PlanStepDto _$PlanStepDtoFromJson(Map<String, dynamic> json) => PlanStepDto(
  sequence: (json['sequence'] as num?)?.toInt(),
  step: (json['step'] as num?)?.toInt(),
  agentRole: json['agent_role'] as String?,
  description: json['description'] as String?,
  status: json['status'] as String?,
  startedAt: json['started_at'] == null
      ? null
      : DateTime.parse(json['started_at'] as String),
  completedAt: json['completed_at'] == null
      ? null
      : DateTime.parse(json['completed_at'] as String),
);

Map<String, dynamic> _$PlanStepDtoToJson(PlanStepDto instance) =>
    <String, dynamic>{
      'sequence': instance.sequence,
      'step': instance.step,
      'agent_role': instance.agentRole,
      'description': instance.description,
      'status': instance.status,
      'started_at': instance.startedAt?.toIso8601String(),
      'completed_at': instance.completedAt?.toIso8601String(),
    };
