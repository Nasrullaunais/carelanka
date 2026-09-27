// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'dispatch_tool_call.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

DispatchToolCall _$DispatchToolCallFromJson(Map<String, dynamic> json) =>
    DispatchToolCall(
      toolName: json['tool_name'] as String?,
      arguments: json['arguments'] as Map<String, dynamic>?,
      succeeded: json['succeeded'] as bool?,
      durationMs: (json['duration_ms'] as num?)?.toInt(),
      error: json['error'] as String?,
      calledAt: json['called_at'] == null
          ? null
          : DateTime.parse(json['called_at'] as String),
    );

Map<String, dynamic> _$DispatchToolCallToJson(DispatchToolCall instance) =>
    <String, dynamic>{
      'tool_name': instance.toolName,
      'arguments': instance.arguments,
      'succeeded': instance.succeeded,
      'duration_ms': instance.durationMs,
      'error': instance.error,
      'called_at': instance.calledAt?.toIso8601String(),
    };
