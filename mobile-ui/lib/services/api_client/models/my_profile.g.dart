// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'my_profile.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

MyProfile _$MyProfileFromJson(Map<String, dynamic> json) => MyProfile(
  patientCode: json['patient_code'] as String,
  fullName: json['full_name'] as String,
  gender: Gender.fromJson(json['gender'] as String),
  detailsComplete: json['details_complete'] as bool,
  missingFields: (json['missing_fields'] as List<dynamic>)
      .map((e) => e as String)
      .toList(),
  nic: json['nic'] as String?,
  dateOfBirth: json['date_of_birth'] == null
      ? null
      : DateTime.parse(json['date_of_birth'] as String),
  phone: json['phone'] as String?,
  address: json['address'] as String?,
  emergencyContactName: json['emergency_contact_name'] as String?,
  emergencyContactPhone: json['emergency_contact_phone'] as String?,
);

Map<String, dynamic> _$MyProfileToJson(MyProfile instance) => <String, dynamic>{
  'patient_code': instance.patientCode,
  'full_name': instance.fullName,
  'nic': instance.nic,
  'gender': instance.gender,
  'date_of_birth': instance.dateOfBirth?.toIso8601String(),
  'phone': instance.phone,
  'address': instance.address,
  'emergency_contact_name': instance.emergencyContactName,
  'emergency_contact_phone': instance.emergencyContactPhone,
  'details_complete': instance.detailsComplete,
  'missing_fields': instance.missingFields,
};
