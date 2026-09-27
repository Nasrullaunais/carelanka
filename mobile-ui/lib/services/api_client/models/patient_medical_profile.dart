// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'patient_medical_profile.g.dart';

@JsonSerializable()
class PatientMedicalProfile {
  const PatientMedicalProfile({
    required this.patientId,
    this.knownConditions,
    this.allergies,
    this.currentSymptoms,
    this.updatedByStaffId,
    this.updatedByStaffName,
    this.updatedAt,
  });
  
  factory PatientMedicalProfile.fromJson(Map<String, Object?> json) => _$PatientMedicalProfileFromJson(json);
  
  @JsonKey(name: 'patient_id')
  final String patientId;
  @JsonKey(name: 'known_conditions')
  final String? knownConditions;
  final String? allergies;
  @JsonKey(name: 'current_symptoms')
  final String? currentSymptoms;
  @JsonKey(name: 'updated_by_staff_id')
  final String? updatedByStaffId;
  @JsonKey(name: 'updated_by_staff_name')
  final String? updatedByStaffName;
  @JsonKey(name: 'updated_at')
  final DateTime? updatedAt;

  Map<String, Object?> toJson() => _$PatientMedicalProfileToJson(this);
}
