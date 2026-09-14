// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'admission_status.dart';

part 'my_admission.g.dart';

@JsonSerializable()
class MyAdmission {
  const MyAdmission({
    required this.admissionId,
    required this.status,
    required this.statusText,
    required this.detailsComplete,
    required this.missingFields,
    this.wardName,
    this.bedNumber,
    this.admittedAt,
    this.expectedArrival,
    this.dischargedAt,
    this.dischargeInstructions,
  });
  
  factory MyAdmission.fromJson(Map<String, Object?> json) => _$MyAdmissionFromJson(json);
  
  @JsonKey(name: 'admission_id')
  final String admissionId;
  final AdmissionStatus status;
  @JsonKey(name: 'status_text')
  final String statusText;
  @JsonKey(name: 'ward_name')
  final String? wardName;
  @JsonKey(name: 'bed_number')
  final String? bedNumber;
  @JsonKey(name: 'admitted_at')
  final DateTime? admittedAt;
  @JsonKey(name: 'expected_arrival')
  final DateTime? expectedArrival;
  @JsonKey(name: 'discharged_at')
  final DateTime? dischargedAt;
  @JsonKey(name: 'discharge_instructions')
  final String? dischargeInstructions;
  @JsonKey(name: 'details_complete')
  final bool detailsComplete;
  @JsonKey(name: 'missing_fields')
  final List<String> missingFields;

  Map<String, Object?> toJson() => _$MyAdmissionToJson(this);
}
