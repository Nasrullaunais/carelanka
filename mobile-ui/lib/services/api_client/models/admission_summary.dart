// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'admission_category.dart';
import 'admission_source.dart';
import 'admission_status.dart';
import 'admission_urgency.dart';
import 'patient_summary.dart';

part 'admission_summary.g.dart';

@JsonSerializable()
class AdmissionSummary {
  const AdmissionSummary({
    required this.id,
    required this.source,
    required this.urgency,
    required this.status,
    required this.detailsComplete,
    required this.requiresBed,
    this.patient,
    this.admissionCategory,
    this.wardName,
    this.bedNumber,
    this.expectedArrival,
    this.admittedAt,
  });
  
  factory AdmissionSummary.fromJson(Map<String, Object?> json) => _$AdmissionSummaryFromJson(json);
  
  final String id;
  final PatientSummary? patient;
  final AdmissionSource source;
  @JsonKey(name: 'admission_category')
  final AdmissionCategory? admissionCategory;
  final AdmissionUrgency urgency;
  final AdmissionStatus status;
  @JsonKey(name: 'details_complete')
  final bool detailsComplete;
  @JsonKey(name: 'requires_bed')
  final bool requiresBed;
  @JsonKey(name: 'ward_name')
  final String? wardName;
  @JsonKey(name: 'bed_number')
  final String? bedNumber;
  @JsonKey(name: 'expected_arrival')
  final DateTime? expectedArrival;
  @JsonKey(name: 'admitted_at')
  final DateTime? admittedAt;

  Map<String, Object?> toJson() => _$AdmissionSummaryToJson(this);
}
