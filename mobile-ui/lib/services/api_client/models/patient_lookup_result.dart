// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'patient_summary.dart';

part 'patient_lookup_result.g.dart';

@JsonSerializable()
class PatientLookupResult {
  const PatientLookupResult({
    required this.found,
    required this.hasOpenAdmission,
    this.patient,
  });
  
  factory PatientLookupResult.fromJson(Map<String, Object?> json) => _$PatientLookupResultFromJson(json);
  
  final bool found;
  final PatientSummary? patient;
  @JsonKey(name: 'has_open_admission')
  final bool hasOpenAdmission;

  Map<String, Object?> toJson() => _$PatientLookupResultToJson(this);
}
