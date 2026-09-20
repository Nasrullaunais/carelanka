// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'bed_agent_step.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

BedAgentStep _$BedAgentStepFromJson(Map<String, dynamic> json) => BedAgentStep(
  step: json['step'] as String?,
  tool: json['tool'] as String?,
  startedAt: json['started_at'] == null
      ? null
      : DateTime.parse(json['started_at'] as String),
  durationMs: (json['duration_ms'] as num?)?.toInt(),
  ok: json['ok'] as bool?,
  error: json['error'] as String?,
);

Map<String, dynamic> _$BedAgentStepToJson(BedAgentStep instance) =>
    <String, dynamic>{
      'step': instance.step,
      'tool': instance.tool,
      'started_at': instance.startedAt?.toIso8601String(),
      'duration_ms': instance.durationMs,
      'ok': instance.ok,
      'error': instance.error,
    };
