// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'patient_register_request.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

PatientRegisterRequest _$PatientRegisterRequestFromJson(
  Map<String, dynamic> json,
) => PatientRegisterRequest(
  phoneNumber: json['phone_number'] as String,
  password: json['password'] as String,
  fullName: json['full_name'] as String,
);

Map<String, dynamic> _$PatientRegisterRequestToJson(
  PatientRegisterRequest instance,
) => <String, dynamic>{
  'phone_number': instance.phoneNumber,
  'password': instance.password,
  'full_name': instance.fullName,
};
