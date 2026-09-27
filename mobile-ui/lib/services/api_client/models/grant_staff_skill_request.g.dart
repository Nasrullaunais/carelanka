// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'grant_staff_skill_request.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

GrantStaffSkillRequest _$GrantStaffSkillRequestFromJson(
  Map<String, dynamic> json,
) => GrantStaffSkillRequest(
  skillId: json['skill_id'] as String,
  validFrom: json['valid_from'] == null
      ? null
      : DateTime.parse(json['valid_from'] as String),
  expiresAt: json['expires_at'] == null
      ? null
      : DateTime.parse(json['expires_at'] as String),
);

Map<String, dynamic> _$GrantStaffSkillRequestToJson(
  GrantStaffSkillRequest instance,
) => <String, dynamic>{
  'skill_id': instance.skillId,
  'valid_from': instance.validFrom?.toIso8601String(),
  'expires_at': instance.expiresAt?.toIso8601String(),
};
