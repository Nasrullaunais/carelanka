// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'pre_register_request.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

PreRegisterRequest _$PreRegisterRequestFromJson(Map<String, dynamic> json) =>
    PreRegisterRequest(
      nic: json['nic'] as String,
      fullName: json['full_name'] as String,
      gender: Gender.fromJson(json['gender'] as String),
      dateOfBirth: json['date_of_birth'] == null
          ? null
          : DateTime.parse(json['date_of_birth'] as String),
      phone: json['phone'] as String?,
      address: json['address'] as String?,
      emergencyContactName: json['emergency_contact_name'] as String?,
      emergencyContactPhone: json['emergency_contact_phone'] as String?,
    );

Map<String, dynamic> _$PreRegisterRequestToJson(PreRegisterRequest instance) =>
    <String, dynamic>{
      'nic': instance.nic,
      'full_name': instance.fullName,
      'gender': instance.gender,
      'date_of_birth': instance.dateOfBirth?.toIso8601String(),
      'phone': instance.phone,
      'address': instance.address,
      'emergency_contact_name': instance.emergencyContactName,
      'emergency_contact_phone': instance.emergencyContactPhone,
    };
