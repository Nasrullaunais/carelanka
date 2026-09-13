// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'patient_summary.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

PatientSummary _$PatientSummaryFromJson(Map<String, dynamic> json) =>
    PatientSummary(
      id: json['id'] as String,
      patientCode: json['patient_code'] as String,
      fullName: json['full_name'] as String,
      gender: Gender.fromJson(json['gender'] as String),
      nic: json['nic'] as String?,
      tempReference: json['temp_reference'] as String?,
      dateOfBirth: json['date_of_birth'] == null
          ? null
          : DateTime.parse(json['date_of_birth'] as String),
    );

Map<String, dynamic> _$PatientSummaryToJson(PatientSummary instance) =>
    <String, dynamic>{
      'id': instance.id,
      'patient_code': instance.patientCode,
      'full_name': instance.fullName,
      'nic': instance.nic,
      'temp_reference': instance.tempReference,
      'gender': instance.gender,
      'date_of_birth': instance.dateOfBirth?.toIso8601String(),
    };
