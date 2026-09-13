// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'create_ward_request.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

CreateWardRequest _$CreateWardRequestFromJson(Map<String, dynamic> json) =>
    CreateWardRequest(
      name: json['name'] as String,
      wardType: WardType.fromJson(json['ward_type'] as String),
      genderPolicy: GenderPolicy.fromJson(json['gender_policy'] as String),
      isActive: json['is_active'] as bool? ?? true,
    );

Map<String, dynamic> _$CreateWardRequestToJson(CreateWardRequest instance) =>
    <String, dynamic>{
      'name': instance.name,
      'ward_type': instance.wardType,
      'gender_policy': instance.genderPolicy,
      'is_active': instance.isActive,
    };
