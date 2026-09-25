// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'patient_medical_profile.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

PatientMedicalProfile _$PatientMedicalProfileFromJson(
  Map<String, dynamic> json,
) => PatientMedicalProfile(
  patientId: json['patient_id'] as String,
  knownConditions: json['known_conditions'] as String?,
  allergies: json['allergies'] as String?,
  currentSymptoms: json['current_symptoms'] as String?,
  updatedByStaffId: json['updated_by_staff_id'] as String?,
  updatedByStaffName: json['updated_by_staff_name'] as String?,
  updatedAt: json['updated_at'] == null
      ? null
      : DateTime.parse(json['updated_at'] as String),
);

Map<String, dynamic> _$PatientMedicalProfileToJson(
  PatientMedicalProfile instance,
) => <String, dynamic>{
  'patient_id': instance.patientId,
  'known_conditions': instance.knownConditions,
  'allergies': instance.allergies,
  'current_symptoms': instance.currentSymptoms,
  'updated_by_staff_id': instance.updatedByStaffId,
  'updated_by_staff_name': instance.updatedByStaffName,
  'updated_at': instance.updatedAt?.toIso8601String(),
};
