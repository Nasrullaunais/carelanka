// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'bed_suggestion_patient.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

BedSuggestionPatient _$BedSuggestionPatientFromJson(
  Map<String, dynamic> json,
) => BedSuggestionPatient(
  patientId: json['patient_id'] as String,
  patientCode: json['patient_code'] as String?,
  fullName: json['full_name'] as String?,
  age: (json['age'] as num?)?.toInt(),
  gender: json['gender'] == null
      ? null
      : Gender.fromJson(json['gender'] as String),
  admissionId: json['admission_id'] as String?,
  admissionCategory: json['admission_category'] == null
      ? null
      : AdmissionCategory.fromJson(json['admission_category'] as String),
  urgency: json['urgency'] == null
      ? null
      : AdmissionUrgency.fromJson(json['urgency'] as String),
  isInfectious: json['is_infectious'] as bool?,
  status: json['status'] == null
      ? null
      : AdmissionStatus.fromJson(json['status'] as String),
);

Map<String, dynamic> _$BedSuggestionPatientToJson(
  BedSuggestionPatient instance,
) => <String, dynamic>{
  'patient_id': instance.patientId,
  'patient_code': instance.patientCode,
  'full_name': instance.fullName,
  'age': instance.age,
  'gender': instance.gender,
  'admission_id': instance.admissionId,
  'admission_category': instance.admissionCategory,
  'urgency': instance.urgency,
  'is_infectious': instance.isInfectious,
  'status': instance.status,
};
