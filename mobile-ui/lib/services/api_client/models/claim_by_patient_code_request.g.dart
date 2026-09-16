// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'claim_by_patient_code_request.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

ClaimByPatientCodeRequest _$ClaimByPatientCodeRequestFromJson(
  Map<String, dynamic> json,
) => ClaimByPatientCodeRequest(
  patientCode: json['patient_code'] as String?,
  nic: json['nic'] as String?,
);

Map<String, dynamic> _$ClaimByPatientCodeRequestToJson(
  ClaimByPatientCodeRequest instance,
) => <String, dynamic>{
  'patient_code': instance.patientCode,
  'nic': instance.nic,
};
