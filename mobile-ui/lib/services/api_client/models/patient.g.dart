// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'patient.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

Patient _$PatientFromJson(Map<String, dynamic> json) => Patient(
  id: json['id'] as String,
  patientCode: json['patient_code'] as String,
  fullName: json['full_name'] as String,
  gender: Gender.fromJson(json['gender'] as String),
  hasAccount: json['has_account'] as bool,
  nic: json['nic'] as String?,
  tempReference: json['temp_reference'] as String?,
  dateOfBirth: json['date_of_birth'] == null
      ? null
      : DateTime.parse(json['date_of_birth'] as String),
  phone: json['phone'] as String?,
  address: json['address'] as String?,
  emergencyContactName: json['emergency_contact_name'] as String?,
  emergencyContactPhone: json['emergency_contact_phone'] as String?,
  createdAt: json['created_at'] == null
      ? null
      : DateTime.parse(json['created_at'] as String),
  updatedAt: json['updated_at'] == null
      ? null
      : DateTime.parse(json['updated_at'] as String),
);

Map<String, dynamic> _$PatientToJson(Patient instance) => <String, dynamic>{
  'id': instance.id,
  'patient_code': instance.patientCode,
  'full_name': instance.fullName,
  'nic': instance.nic,
  'temp_reference': instance.tempReference,
  'gender': instance.gender,
  'date_of_birth': instance.dateOfBirth?.toIso8601String(),
  'phone': instance.phone,
  'address': instance.address,
  'emergency_contact_name': instance.emergencyContactName,
  'emergency_contact_phone': instance.emergencyContactPhone,
  'has_account': instance.hasAccount,
  'created_at': instance.createdAt?.toIso8601String(),
  'updated_at': instance.updatedAt?.toIso8601String(),
};
