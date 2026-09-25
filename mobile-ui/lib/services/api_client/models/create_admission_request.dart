// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'admission_category.dart';
import 'admission_source.dart';
import 'admission_urgency.dart';

part 'create_admission_request.g.dart';

@JsonSerializable()
class CreateAdmissionRequest {
  const CreateAdmissionRequest({
    required this.patientId,
    required this.source,
    required this.admissionCategory,
    required this.urgency,
    this.isInfectious = false,
    this.dispatchId,
    this.expectedArrival,
  });
  
  factory CreateAdmissionRequest.fromJson(Map<String, Object?> json) => _$CreateAdmissionRequestFromJson(json);
  
  @JsonKey(name: 'patient_id')
  final String patientId;
  final AdmissionSource source;
  @JsonKey(name: 'dispatch_id')
  final String? dispatchId;
  @JsonKey(name: 'admission_category')
  final AdmissionCategory admissionCategory;
  final AdmissionUrgency urgency;
  @JsonKey(name: 'is_infectious')
  final bool isInfectious;
  @JsonKey(name: 'expected_arrival')
  final DateTime? expectedArrival;

  Map<String, Object?> toJson() => _$CreateAdmissionRequestToJson(this);
}
