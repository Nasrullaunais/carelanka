// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'plan_step_dto.g.dart';

@JsonSerializable()
class PlanStepDto {
  const PlanStepDto({
    this.sequence,
    this.step,
    this.agentRole,
    this.description,
    this.status,
    this.startedAt,
    this.completedAt,
  });
  
  factory PlanStepDto.fromJson(Map<String, Object?> json) => _$PlanStepDtoFromJson(json);
  
  final int? sequence;
  final int? step;
  @JsonKey(name: 'agent_role')
  final String? agentRole;
  final String? description;
  final String? status;
  @JsonKey(name: 'started_at')
  final DateTime? startedAt;
  @JsonKey(name: 'completed_at')
  final DateTime? completedAt;

  Map<String, Object?> toJson() => _$PlanStepDtoToJson(this);
}
