// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'patient_claim_preview.g.dart';

@JsonSerializable()
class PatientClaimPreview {
  const PatientClaimPreview({
    required this.patientCode,
    required this.maskedFullName,
    this.maskedPhone,
  });
  
  factory PatientClaimPreview.fromJson(Map<String, Object?> json) => _$PatientClaimPreviewFromJson(json);
  
  @JsonKey(name: 'patient_code')
  final String patientCode;
  @JsonKey(name: 'masked_full_name')
  final String maskedFullName;
  @JsonKey(name: 'masked_phone')
  final String? maskedPhone;

  Map<String, Object?> toJson() => _$PatientClaimPreviewToJson(this);
}
