// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'complete_details_request.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

CompleteDetailsRequest _$CompleteDetailsRequestFromJson(
  Map<String, dynamic> json,
) => CompleteDetailsRequest(
  nic: json['nic'] as String?,
  fullName: json['full_name'] as String?,
  dateOfBirth: json['date_of_birth'] == null
      ? null
      : DateTime.parse(json['date_of_birth'] as String),
  phone: json['phone'] as String?,
  address: json['address'] as String?,
  emergencyContactName: json['emergency_contact_name'] as String?,
  emergencyContactPhone: json['emergency_contact_phone'] as String?,
);

Map<String, dynamic> _$CompleteDetailsRequestToJson(
  CompleteDetailsRequest instance,
) => <String, dynamic>{
  'nic': instance.nic,
  'full_name': instance.fullName,
  'date_of_birth': instance.dateOfBirth?.toIso8601String(),
  'phone': instance.phone,
  'address': instance.address,
  'emergency_contact_name': instance.emergencyContactName,
  'emergency_contact_phone': instance.emergencyContactPhone,
};
