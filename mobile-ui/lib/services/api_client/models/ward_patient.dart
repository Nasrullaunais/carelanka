// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'admission_status.dart';

part 'ward_patient.g.dart';

@JsonSerializable()
class WardPatient {
  const WardPatient({
    required this.admissionId,
    required this.patientId,
    required this.patientCode,
    required this.fullName,
    required this.admissionStatus,
    this.wardName,
    this.bedNumber,
  });
  
  factory WardPatient.fromJson(Map<String, Object?> json) => _$WardPatientFromJson(json);
  
  @JsonKey(name: 'admission_id')
  final String admissionId;
  @JsonKey(name: 'patient_id')
  final String patientId;
  @JsonKey(name: 'patient_code')
  final String patientCode;
  @JsonKey(name: 'full_name')
  final String fullName;
  @JsonKey(name: 'ward_name')
  final String? wardName;
  @JsonKey(name: 'bed_number')
  final String? bedNumber;
  @JsonKey(name: 'admission_status')
  final AdmissionStatus admissionStatus;

  Map<String, Object?> toJson() => _$WardPatientToJson(this);
}
