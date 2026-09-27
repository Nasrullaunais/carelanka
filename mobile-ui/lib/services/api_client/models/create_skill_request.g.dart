// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'create_skill_request.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

CreateSkillRequest _$CreateSkillRequestFromJson(Map<String, dynamic> json) =>
    CreateSkillRequest(
      name: json['name'] as String,
      description: json['description'] as String?,
    );

Map<String, dynamic> _$CreateSkillRequestToJson(CreateSkillRequest instance) =>
    <String, dynamic>{
      'name': instance.name,
      'description': instance.description,
    };
