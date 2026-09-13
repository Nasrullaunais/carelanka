// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'admission_category.dart';
import 'patient_summary.dart';

part 'discharge_candidate.g.dart';

@JsonSerializable()
class DischargeCandidate {
  const DischargeCandidate({
    required this.admissionId,
    required this.patient,
    required this.wardName,
    required this.bedNumber,
    required this.admissionCategory,
    required this.daysInBed,
    required this.outstandingItems,
    required this.isDischarged,
    this.admittedAt,
    this.dischargedAt,
  });
  
  factory DischargeCandidate.fromJson(Map<String, Object?> json) => _$DischargeCandidateFromJson(json);
  
  @JsonKey(name: 'admission_id')
  final String admissionId;
  final PatientSummary patient;
  @JsonKey(name: 'ward_name')
  final String wardName;
  @JsonKey(name: 'bed_number')
  final String bedNumber;
  @JsonKey(name: 'admission_category')
  final AdmissionCategory admissionCategory;
  @JsonKey(name: 'admitted_at')
  final DateTime? admittedAt;
  @JsonKey(name: 'days_in_bed')
  final int daysInBed;
  @JsonKey(name: 'outstanding_items')
  final List<String> outstandingItems;
  @JsonKey(name: 'is_discharged')
  final bool isDischarged;
  @JsonKey(name: 'discharged_at')
  final DateTime? dischargedAt;

  Map<String, Object?> toJson() => _$DischargeCandidateToJson(this);
}
