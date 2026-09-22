// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'create_emergency_call_request.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

CreateEmergencyCallRequest _$CreateEmergencyCallRequestFromJson(
  Map<String, dynamic> json,
) => CreateEmergencyCallRequest(
  patientIsCaller: json['patient_is_caller'] as bool,
  latitude: (json['latitude'] as num).toDouble(),
  longitude: (json['longitude'] as num).toDouble(),
  locationAccuracyMetres: (json['location_accuracy_metres'] as num).toDouble(),
  locationCapturedAt: DateTime.parse(json['location_captured_at'] as String),
  idempotencyKey: json['idempotency_key'] as String,
  patientId: json['patient_id'] as String?,
  callerName: json['caller_name'] as String?,
  callerPhone: json['caller_phone'] as String?,
  details: json['details'] as String?,
  priority: json['priority'] == null
      ? null
      : CallPriority.fromJson(json['priority'] as String),
);

Map<String, dynamic> _$CreateEmergencyCallRequestToJson(
  CreateEmergencyCallRequest instance,
) => <String, dynamic>{
  'patient_is_caller': instance.patientIsCaller,
  'patient_id': instance.patientId,
  'caller_name': instance.callerName,
  'caller_phone': instance.callerPhone,
  'latitude': instance.latitude,
  'longitude': instance.longitude,
  'location_accuracy_metres': instance.locationAccuracyMetres,
  'location_captured_at': instance.locationCapturedAt.toIso8601String(),
  'idempotency_key': instance.idempotencyKey,
  'details': instance.details,
  'priority': instance.priority,
};
