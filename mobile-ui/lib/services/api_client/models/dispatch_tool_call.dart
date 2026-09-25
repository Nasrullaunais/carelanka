// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'dispatch_tool_call.g.dart';

@JsonSerializable()
class DispatchToolCall {
  const DispatchToolCall({
    this.toolName,
    this.arguments,
    this.succeeded,
    this.durationMs,
    this.error,
    this.calledAt,
  });

  factory DispatchToolCall.fromJson(Map<String, Object?> json) =>
      _$DispatchToolCallFromJson(json);

  @JsonKey(name: 'tool_name')
  final String? toolName;
  final Map<String, dynamic>? arguments;
  final bool? succeeded;
  @JsonKey(name: 'duration_ms')
  final int? durationMs;
  final String? error;
  @JsonKey(name: 'called_at')
  final DateTime? calledAt;

  Map<String, Object?> toJson() => _$DispatchToolCallToJson(this);
}
