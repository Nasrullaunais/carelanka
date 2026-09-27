// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'patient_app_password_reset.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

PatientAppPasswordReset _$PatientAppPasswordResetFromJson(
  Map<String, dynamic> json,
) => PatientAppPasswordReset(
  username: json['username'] as String,
  temporaryPassword: json['temporary_password'] as String,
);

Map<String, dynamic> _$PatientAppPasswordResetToJson(
  PatientAppPasswordReset instance,
) => <String, dynamic>{
  'username': instance.username,
  'temporary_password': instance.temporaryPassword,
};
