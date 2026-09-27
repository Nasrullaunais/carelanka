// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'staff_role.dart';

part 'ward_staffing_rule_dto.g.dart';

@JsonSerializable()
class WardStaffingRuleDto {
  const WardStaffingRuleDto({
    required this.id,
    required this.wardId,
    required this.requiredRole,
    required this.minimumHeadcount,
    this.requiredSkillId,
    this.requiredSkillName,
  });
  
  factory WardStaffingRuleDto.fromJson(Map<String, Object?> json) => _$WardStaffingRuleDtoFromJson(json);
  
  final String id;
  @JsonKey(name: 'ward_id')
  final String wardId;
  @JsonKey(name: 'required_role')
  final StaffRole requiredRole;
  @JsonKey(name: 'required_skill_id')
  final String? requiredSkillId;
  @JsonKey(name: 'required_skill_name')
  final String? requiredSkillName;
  @JsonKey(name: 'minimum_headcount')
  final int minimumHeadcount;

  Map<String, Object?> toJson() => _$WardStaffingRuleDtoToJson(this);
}
