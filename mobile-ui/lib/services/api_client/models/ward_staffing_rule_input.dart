// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'staff_role.dart';

part 'ward_staffing_rule_input.g.dart';

@JsonSerializable()
class WardStaffingRuleInput {
  const WardStaffingRuleInput({
    required this.requiredRole,
    required this.minimumHeadcount,
    this.requiredSkillId,
  });
  
  factory WardStaffingRuleInput.fromJson(Map<String, Object?> json) => _$WardStaffingRuleInputFromJson(json);
  
  @JsonKey(name: 'required_role')
  final StaffRole requiredRole;
  @JsonKey(name: 'required_skill_id')
  final String? requiredSkillId;
  @JsonKey(name: 'minimum_headcount')
  final int minimumHeadcount;

  Map<String, Object?> toJson() => _$WardStaffingRuleInputToJson(this);
}
