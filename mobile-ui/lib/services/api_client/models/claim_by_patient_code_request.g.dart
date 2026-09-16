// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'claim_by_patient_code_request.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

ClaimByPatientCodeRequest _$ClaimByPatientCodeRequestFromJson(
  Map<String, dynamic> json,
) => ClaimByPatientCodeRequest(
  patientCode: json['patient_code'] as String?,
  dateOfBirth: json['date_of_birth'] == null
      ? null
      : DateTime.parse(json['date_of_birth'] as String),
);

Map<String, dynamic> _$ClaimByPatientCodeRequestToJson(
  ClaimByPatientCodeRequest instance,
) => <String, dynamic>{
  'patient_code': instance.patientCode,
  'date_of_birth': instance.dateOfBirth?.toIso8601String(),
};
