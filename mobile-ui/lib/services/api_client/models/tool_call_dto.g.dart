// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'tool_call_dto.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

ToolCallDto _$ToolCallDtoFromJson(Map<String, dynamic> json) => ToolCallDto(
  toolName: json['tool_name'] as String?,
  tool: json['tool'] as String?,
  arguments: json['arguments'] as Map<String, dynamic>?,
  succeeded: json['succeeded'] as bool?,
  durationMs: (json['duration_ms'] as num?)?.toInt(),
  error: json['error'] as String?,
  summary: json['summary'] as String?,
  calledAt: json['called_at'] == null
      ? null
      : DateTime.parse(json['called_at'] as String),
);

Map<String, dynamic> _$ToolCallDtoToJson(ToolCallDto instance) =>
    <String, dynamic>{
      'tool_name': instance.toolName,
      'tool': instance.tool,
      'arguments': instance.arguments,
      'succeeded': instance.succeeded,
      'duration_ms': instance.durationMs,
      'error': instance.error,
      'summary': instance.summary,
      'called_at': instance.calledAt?.toIso8601String(),
    };
