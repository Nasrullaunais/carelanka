// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'staff_role.dart';

part 'bulk_shift_pattern_item.g.dart';

@JsonSerializable()
class BulkShiftPatternItem {
  const BulkShiftPatternItem({
    required this.startTime,
    required this.endTime,
    required this.requiredRole,
    required this.headcountNeeded,
    this.requiredSkillId,
    this.minimumHeadcount,
  });
  
  factory BulkShiftPatternItem.fromJson(Map<String, Object?> json) => _$BulkShiftPatternItemFromJson(json);
  
  @JsonKey(name: 'start_time')
  final String startTime;
  @JsonKey(name: 'end_time')
  final String endTime;
  @JsonKey(name: 'required_role')
  final StaffRole requiredRole;
  @JsonKey(name: 'required_skill_id')
  final String? requiredSkillId;
  @JsonKey(name: 'headcount_needed')
  final int headcountNeeded;
  @JsonKey(name: 'minimum_headcount')
  final int? minimumHeadcount;

  Map<String, Object?> toJson() => _$BulkShiftPatternItemToJson(this);
}
