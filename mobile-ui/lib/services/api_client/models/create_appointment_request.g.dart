// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'create_appointment_request.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

CreateAppointmentRequest _$CreateAppointmentRequestFromJson(
  Map<String, dynamic> json,
) => CreateAppointmentRequest(
  patientId: json['patient_id'] as String,
  scheduledAt: DateTime.parse(json['scheduled_at'] as String),
  reason: json['reason'] as String?,
);

Map<String, dynamic> _$CreateAppointmentRequestToJson(
  CreateAppointmentRequest instance,
) => <String, dynamic>{
  'patient_id': instance.patientId,
  'scheduled_at': instance.scheduledAt.toIso8601String(),
  'reason': instance.reason,
};
