// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'staff_skill_dto.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

StaffSkillDto _$StaffSkillDtoFromJson(Map<String, dynamic> json) =>
    StaffSkillDto(
      skillId: json['skill_id'] as String,
      skillName: json['skill_name'] as String,
      isValid: json['is_valid'] as bool,
      grantedAt: DateTime.parse(json['granted_at'] as String),
      validFrom: json['valid_from'] == null
          ? null
          : DateTime.parse(json['valid_from'] as String),
      expiresAt: json['expires_at'] == null
          ? null
          : DateTime.parse(json['expires_at'] as String),
    );

Map<String, dynamic> _$StaffSkillDtoToJson(StaffSkillDto instance) =>
    <String, dynamic>{
      'skill_id': instance.skillId,
      'skill_name': instance.skillName,
      'valid_from': instance.validFrom?.toIso8601String(),
      'expires_at': instance.expiresAt?.toIso8601String(),
      'is_valid': instance.isValid,
      'granted_at': instance.grantedAt.toIso8601String(),
    };
