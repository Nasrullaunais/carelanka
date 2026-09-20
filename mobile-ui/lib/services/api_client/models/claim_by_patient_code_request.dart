// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'claim_by_patient_code_request.g.dart';

@JsonSerializable()
class ClaimByPatientCodeRequest {
  const ClaimByPatientCodeRequest({
    this.patientCode,
    this.nic,
  });
  
  factory ClaimByPatientCodeRequest.fromJson(Map<String, Object?> json) => _$ClaimByPatientCodeRequestFromJson(json);
  
  @JsonKey(name: 'patient_code')
  final String? patientCode;
  final String? nic;

  Map<String, Object?> toJson() => _$ClaimByPatientCodeRequestToJson(this);
}
