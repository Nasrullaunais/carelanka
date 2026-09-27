// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'create_walk_in_appointment_request.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

CreateWalkInAppointmentRequest _$CreateWalkInAppointmentRequestFromJson(
  Map<String, dynamic> json,
) => CreateWalkInAppointmentRequest(
  patientId: json['patient_id'] as String,
  reason: json['reason'] as String?,
);

Map<String, dynamic> _$CreateWalkInAppointmentRequestToJson(
  CreateWalkInAppointmentRequest instance,
) => <String, dynamic>{
  'patient_id': instance.patientId,
  'reason': instance.reason,
};
