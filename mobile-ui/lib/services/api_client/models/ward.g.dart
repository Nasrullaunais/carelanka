// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'ward.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

Ward _$WardFromJson(Map<String, dynamic> json) => Ward(
  id: json['id'] as String,
  name: json['name'] as String,
  wardType: WardType.fromJson(json['ward_type'] as String),
  genderPolicy: GenderPolicy.fromJson(json['gender_policy'] as String),
  isActive: json['is_active'] as bool,
  totalBeds: (json['total_beds'] as num).toInt(),
  createdAt: json['created_at'] == null
      ? null
      : DateTime.parse(json['created_at'] as String),
  updatedAt: json['updated_at'] == null
      ? null
      : DateTime.parse(json['updated_at'] as String),
);

Map<String, dynamic> _$WardToJson(Ward instance) => <String, dynamic>{
  'id': instance.id,
  'name': instance.name,
  'ward_type': instance.wardType,
  'gender_policy': instance.genderPolicy,
  'is_active': instance.isActive,
  'total_beds': instance.totalBeds,
  'created_at': instance.createdAt?.toIso8601String(),
  'updated_at': instance.updatedAt?.toIso8601String(),
};
