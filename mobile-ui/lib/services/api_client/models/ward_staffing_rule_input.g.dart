// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'ward_staffing_rule_input.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

WardStaffingRuleInput _$WardStaffingRuleInputFromJson(
  Map<String, dynamic> json,
) => WardStaffingRuleInput(
  requiredRole: StaffRole.fromJson(json['required_role'] as String),
  minimumHeadcount: (json['minimum_headcount'] as num).toInt(),
  requiredSkillId: json['required_skill_id'] as String?,
);

Map<String, dynamic> _$WardStaffingRuleInputToJson(
  WardStaffingRuleInput instance,
) => <String, dynamic>{
  'required_role': instance.requiredRole,
  'required_skill_id': instance.requiredSkillId,
  'minimum_headcount': instance.minimumHeadcount,
};
