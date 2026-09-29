// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'staff_role.dart';

part 'staff_summary_dto.g.dart';

@JsonSerializable()
class StaffSummaryDto {
  const StaffSummaryDto({
    required this.id,
    required this.fullName,
    required this.role,
    required this.isActive,
    required this.skillCount,
    this.department,
    this.specialization,
  });
  
  factory StaffSummaryDto.fromJson(Map<String, Object?> json) => _$StaffSummaryDtoFromJson(json);
  
  final String id;
  @JsonKey(name: 'full_name')
  final String fullName;
  final StaffRole role;
  final String? department;
  final String? specialization;
  @JsonKey(name: 'is_active')
  final bool isActive;
  @JsonKey(name: 'skill_count')
  final int skillCount;

  Map<String, Object?> toJson() => _$StaffSummaryDtoToJson(this);
}
