// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'create_patient_request.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

CreatePatientRequest _$CreatePatientRequestFromJson(
  Map<String, dynamic> json,
) => CreatePatientRequest(
  fullName: json['full_name'] as String,
  gender: Gender.fromJson(json['gender'] as String),
  nic: json['nic'] as String?,
  dateOfBirth: json['date_of_birth'] == null
      ? null
      : DateTime.parse(json['date_of_birth'] as String),
  phone: json['phone'] as String?,
  address: json['address'] as String?,
  emergencyContactName: json['emergency_contact_name'] as String?,
  emergencyContactPhone: json['emergency_contact_phone'] as String?,
);

Map<String, dynamic> _$CreatePatientRequestToJson(
  CreatePatientRequest instance,
) => <String, dynamic>{
  'full_name': instance.fullName,
  'nic': instance.nic,
  'gender': instance.gender,
  'date_of_birth': instance.dateOfBirth?.toIso8601String(),
  'phone': instance.phone,
  'address': instance.address,
  'emergency_contact_name': instance.emergencyContactName,
  'emergency_contact_phone': instance.emergencyContactPhone,
};
