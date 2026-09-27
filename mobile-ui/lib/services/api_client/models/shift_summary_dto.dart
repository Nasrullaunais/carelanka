// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'shift_coverage_dto.dart';
import 'staff_role.dart';

part 'shift_summary_dto.g.dart';

@JsonSerializable()
class ShiftSummaryDto {
  const ShiftSummaryDto({
    required this.id,
    required this.wardId,
    required this.wardName,
    required this.date,
    required this.startTime,
    required this.endTime,
    required this.crossesMidnight,
    required this.requiredRole,
    required this.headcountNeeded,
    required this.minimumHeadcount,
    required this.createdAt,
    required this.updatedAt,
    required this.coverage,
    this.requiredSkillId,
    this.requiredSkillName,
  });
  
  factory ShiftSummaryDto.fromJson(Map<String, Object?> json) => _$ShiftSummaryDtoFromJson(json);
  
  final String id;
  @JsonKey(name: 'ward_id')
  final String wardId;
  @JsonKey(name: 'ward_name')
  final String wardName;
  final DateTime date;
  @JsonKey(name: 'start_time')
  final String startTime;
  @JsonKey(name: 'end_time')
  final String endTime;
  @JsonKey(name: 'crosses_midnight')
  final bool crossesMidnight;
  @JsonKey(name: 'required_role')
  final StaffRole requiredRole;
  @JsonKey(name: 'required_skill_id')
  final String? requiredSkillId;
  @JsonKey(name: 'required_skill_name')
  final String? requiredSkillName;
  @JsonKey(name: 'headcount_needed')
  final int headcountNeeded;
  @JsonKey(name: 'minimum_headcount')
  final int minimumHeadcount;
  @JsonKey(name: 'created_at')
  final DateTime createdAt;
  @JsonKey(name: 'updated_at')
  final DateTime updatedAt;
  final ShiftCoverageDto coverage;

  Map<String, Object?> toJson() => _$ShiftSummaryDtoToJson(this);
}
