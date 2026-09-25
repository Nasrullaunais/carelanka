// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'dispatch_plan_step.g.dart';

@JsonSerializable()
class DispatchPlanStep {
  const DispatchPlanStep({
    this.sequence,
    this.description,
    this.status,
    this.startedAt,
    this.completedAt,
  });
  
  factory DispatchPlanStep.fromJson(Map<String, Object?> json) => _$DispatchPlanStepFromJson(json);
  
  final int? sequence;
  final String? description;
  final String? status;
  @JsonKey(name: 'started_at')
  final DateTime? startedAt;
  @JsonKey(name: 'completed_at')
  final DateTime? completedAt;

  Map<String, Object?> toJson() => _$DispatchPlanStepToJson(this);
}
