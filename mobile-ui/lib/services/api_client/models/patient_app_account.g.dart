// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'patient_app_account.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

PatientAppAccount _$PatientAppAccountFromJson(Map<String, dynamic> json) =>
    PatientAppAccount(
      patientId: json['patient_id'] as String,
      patientCode: json['patient_code'] as String,
      fullName: json['full_name'] as String,
      username: json['username'] as String,
      nic: json['nic'] as String?,
      dateOfBirth: json['date_of_birth'] == null
          ? null
          : DateTime.parse(json['date_of_birth'] as String),
    );

Map<String, dynamic> _$PatientAppAccountToJson(PatientAppAccount instance) =>
    <String, dynamic>{
      'patient_id': instance.patientId,
      'patient_code': instance.patientCode,
      'full_name': instance.fullName,
      'nic': instance.nic,
      'date_of_birth': instance.dateOfBirth?.toIso8601String(),
      'username': instance.username,
    };
