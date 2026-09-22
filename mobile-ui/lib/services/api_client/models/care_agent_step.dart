// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'care_agent_step.g.dart';

@JsonSerializable()
class CareAgentStep {
  const CareAgentStep({
    this.step,
    this.tool,
    this.startedAt,
    this.durationMs,
    this.ok,
    this.error,
  });
  
  factory CareAgentStep.fromJson(Map<String, Object?> json) => _$CareAgentStepFromJson(json);
  
  final String? step;
  final String? tool;
  @JsonKey(name: 'started_at')
  final DateTime? startedAt;
  @JsonKey(name: 'duration_ms')
  final int? durationMs;
  final bool? ok;
  final String? error;

  Map<String, Object?> toJson() => _$CareAgentStepToJson(this);
}
