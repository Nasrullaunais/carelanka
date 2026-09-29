// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'ward_staffing_rule_dto.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

WardStaffingRuleDto _$WardStaffingRuleDtoFromJson(Map<String, dynamic> json) =>
    WardStaffingRuleDto(
      id: json['id'] as String,
      wardId: json['ward_id'] as String,
      requiredRole: StaffRole.fromJson(json['required_role'] as String),
      minimumHeadcount: (json['minimum_headcount'] as num).toInt(),
      requiredSkillId: json['required_skill_id'] as String?,
      requiredSkillName: json['required_skill_name'] as String?,
    );

Map<String, dynamic> _$WardStaffingRuleDtoToJson(
  WardStaffingRuleDto instance,
) => <String, dynamic>{
  'id': instance.id,
  'ward_id': instance.wardId,
  'required_role': instance.requiredRole,
  'required_skill_id': instance.requiredSkillId,
  'required_skill_name': instance.requiredSkillName,
  'minimum_headcount': instance.minimumHeadcount,
};
