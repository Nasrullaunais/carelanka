// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'ambulance.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

Ambulance _$AmbulanceFromJson(Map<String, dynamic> json) => Ambulance(
  id: json['id'] as String,
  registrationNumber: json['registration_number'] as String,
  status: AmbulanceStatus.fromJson(json['status'] as String),
  isActive: json['is_active'] as bool,
  createdAt: DateTime.parse(json['created_at'] as String),
  updatedAt: DateTime.parse(json['updated_at'] as String),
  currentLatitude: (json['current_latitude'] as num?)?.toDouble(),
  currentLongitude: (json['current_longitude'] as num?)?.toDouble(),
  outOfServiceReason: json['out_of_service_reason'] as String?,
);

Map<String, dynamic> _$AmbulanceToJson(Ambulance instance) => <String, dynamic>{
  'id': instance.id,
  'registration_number': instance.registrationNumber,
  'current_latitude': instance.currentLatitude,
  'current_longitude': instance.currentLongitude,
  'status': instance.status,
  'out_of_service_reason': instance.outOfServiceReason,
  'is_active': instance.isActive,
  'created_at': instance.createdAt.toIso8601String(),
  'updated_at': instance.updatedAt.toIso8601String(),
};
