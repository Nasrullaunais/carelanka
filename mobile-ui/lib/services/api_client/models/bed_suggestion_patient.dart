// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'admission_category.dart';
import 'admission_status.dart';
import 'admission_urgency.dart';
import 'gender.dart';

part 'bed_suggestion_patient.g.dart';

@JsonSerializable()
class BedSuggestionPatient {
  const BedSuggestionPatient({
    required this.patientId,
    required this.patientCode,
    required this.fullName,
    this.age,
    this.gender,
    this.admissionId,
    this.admissionCategory,
    this.urgency,
    this.isInfectious,
    this.status,
  });
  
  factory BedSuggestionPatient.fromJson(Map<String, Object?> json) => _$BedSuggestionPatientFromJson(json);
  
  @JsonKey(name: 'patient_id')
  final String patientId;
  @JsonKey(name: 'patient_code')
  final String? patientCode;
  @JsonKey(name: 'full_name')
  final String? fullName;
  final int? age;
  final Gender? gender;
  @JsonKey(name: 'admission_id')
  final String? admissionId;
  @JsonKey(name: 'admission_category')
  final AdmissionCategory? admissionCategory;
  final AdmissionUrgency? urgency;
  @JsonKey(name: 'is_infectious')
  final bool? isInfectious;
  final AdmissionStatus? status;

  Map<String, Object?> toJson() => _$BedSuggestionPatientToJson(this);
}
