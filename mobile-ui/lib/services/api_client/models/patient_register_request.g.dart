// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'patient_register_request.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

PatientRegisterRequest _$PatientRegisterRequestFromJson(
  Map<String, dynamic> json,
) => PatientRegisterRequest(
  username: json['username'] as String,
  password: json['password'] as String,
);

Map<String, dynamic> _$PatientRegisterRequestToJson(
  PatientRegisterRequest instance,
) => <String, dynamic>{
  'username': instance.username,
  'password': instance.password,
};
