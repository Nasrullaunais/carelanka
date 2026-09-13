// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'gender.dart';

part 'patient_summary.g.dart';

@JsonSerializable()
class PatientSummary {
  const PatientSummary({
    required this.id,
    required this.patientCode,
    required this.fullName,
    required this.gender,
    this.nic,
    this.tempReference,
    this.dateOfBirth,
  });
  
  factory PatientSummary.fromJson(Map<String, Object?> json) => _$PatientSummaryFromJson(json);
  
  final String id;
  @JsonKey(name: 'patient_code')
  final String patientCode;
  @JsonKey(name: 'full_name')
  final String fullName;
  final String? nic;
  @JsonKey(name: 'temp_reference')
  final String? tempReference;
  final Gender gender;
  @JsonKey(name: 'date_of_birth')
  final DateTime? dateOfBirth;

  Map<String, Object?> toJson() => _$PatientSummaryToJson(this);
}
