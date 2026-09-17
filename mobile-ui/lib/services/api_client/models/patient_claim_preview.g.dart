// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'patient_claim_preview.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

PatientClaimPreview _$PatientClaimPreviewFromJson(Map<String, dynamic> json) =>
    PatientClaimPreview(
      patientCode: json['patient_code'] as String,
      maskedFullName: json['masked_full_name'] as String,
      maskedPhone: json['masked_phone'] as String?,
    );

Map<String, dynamic> _$PatientClaimPreviewToJson(
  PatientClaimPreview instance,
) => <String, dynamic>{
  'patient_code': instance.patientCode,
  'masked_full_name': instance.maskedFullName,
  'masked_phone': instance.maskedPhone,
};
