// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'skill_dto.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

SkillDto _$SkillDtoFromJson(Map<String, dynamic> json) => SkillDto(
  id: json['id'] as String,
  name: json['name'] as String,
  staffCount: (json['staff_count'] as num).toInt(),
  description: json['description'] as String?,
);

Map<String, dynamic> _$SkillDtoToJson(SkillDto instance) => <String, dynamic>{
  'id': instance.id,
  'name': instance.name,
  'description': instance.description,
  'staff_count': instance.staffCount,
};
