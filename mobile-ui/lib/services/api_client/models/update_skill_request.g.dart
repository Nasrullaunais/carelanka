// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'update_skill_request.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

UpdateSkillRequest _$UpdateSkillRequestFromJson(Map<String, dynamic> json) =>
    UpdateSkillRequest(
      name: json['name'] as String,
      description: json['description'] as String?,
    );

Map<String, dynamic> _$UpdateSkillRequestToJson(UpdateSkillRequest instance) =>
    <String, dynamic>{
      'name': instance.name,
      'description': instance.description,
    };
