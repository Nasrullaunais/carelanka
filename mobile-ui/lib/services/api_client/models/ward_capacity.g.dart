// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'ward_capacity.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

WardCapacity _$WardCapacityFromJson(Map<String, dynamic> json) => WardCapacity(
  wardId: json['ward_id'] as String,
  name: json['name'] as String,
  wardType: WardType.fromJson(json['ward_type'] as String),
  genderPolicy: GenderPolicy.fromJson(json['gender_policy'] as String),
  totalBeds: (json['total_beds'] as num).toInt(),
  freeBeds: (json['free_beds'] as num).toInt(),
);

Map<String, dynamic> _$WardCapacityToJson(WardCapacity instance) =>
    <String, dynamic>{
      'ward_id': instance.wardId,
      'name': instance.name,
      'ward_type': instance.wardType,
      'gender_policy': instance.genderPolicy,
      'total_beds': instance.totalBeds,
      'free_beds': instance.freeBeds,
    };
