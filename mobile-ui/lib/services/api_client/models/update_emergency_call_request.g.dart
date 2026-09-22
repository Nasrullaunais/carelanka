// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'update_emergency_call_request.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

UpdateEmergencyCallRequest _$UpdateEmergencyCallRequestFromJson(
  Map<String, dynamic> json,
) => UpdateEmergencyCallRequest(
  priority: json['priority'] == null
      ? null
      : CallPriority.fromJson(json['priority'] as String),
  details: json['details'] as String?,
  callerName: json['caller_name'] as String?,
  callerPhone: json['caller_phone'] as String?,
  latitude: (json['latitude'] as num?)?.toDouble(),
  longitude: (json['longitude'] as num?)?.toDouble(),
);

Map<String, dynamic> _$UpdateEmergencyCallRequestToJson(
  UpdateEmergencyCallRequest instance,
) => <String, dynamic>{
  'priority': instance.priority,
  'details': instance.details,
  'caller_name': instance.callerName,
  'caller_phone': instance.callerPhone,
  'latitude': instance.latitude,
  'longitude': instance.longitude,
};
