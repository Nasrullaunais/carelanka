// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'dispatch_plan_step.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

DispatchPlanStep _$DispatchPlanStepFromJson(Map<String, dynamic> json) =>
    DispatchPlanStep(
      sequence: (json['sequence'] as num?)?.toInt(),
      description: json['description'] as String?,
      status: json['status'] as String?,
      startedAt: json['started_at'] == null
          ? null
          : DateTime.parse(json['started_at'] as String),
      completedAt: json['completed_at'] == null
          ? null
          : DateTime.parse(json['completed_at'] as String),
    );

Map<String, dynamic> _$DispatchPlanStepToJson(DispatchPlanStep instance) =>
    <String, dynamic>{
      'sequence': instance.sequence,
      'description': instance.description,
      'status': instance.status,
      'started_at': instance.startedAt?.toIso8601String(),
      'completed_at': instance.completedAt?.toIso8601String(),
    };
