// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'patient_login_request.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

PatientLoginRequest _$PatientLoginRequestFromJson(Map<String, dynamic> json) =>
    PatientLoginRequest(
      username: json['username'] as String,
      password: json['password'] as String,
    );

Map<String, dynamic> _$PatientLoginRequestToJson(
  PatientLoginRequest instance,
) => <String, dynamic>{
  'username': instance.username,
  'password': instance.password,
};
