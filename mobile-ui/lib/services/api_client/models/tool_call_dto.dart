// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'tool_call_dto.g.dart';

@JsonSerializable()
class ToolCallDto {
  const ToolCallDto({
    this.toolName,
    this.tool,
    this.arguments,
    this.succeeded,
    this.durationMs,
    this.error,
    this.summary,
    this.calledAt,
  });
  
  factory ToolCallDto.fromJson(Map<String, Object?> json) => _$ToolCallDtoFromJson(json);
  
  @JsonKey(name: 'tool_name')
  final String? toolName;
  final String? tool;
  final Map<String, dynamic>? arguments;
  final bool? succeeded;
  @JsonKey(name: 'duration_ms')
  final int? durationMs;
  final String? error;
  final String? summary;
  @JsonKey(name: 'called_at')
  final DateTime? calledAt;

  Map<String, Object?> toJson() => _$ToolCallDtoToJson(this);
}
