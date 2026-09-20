// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'bed_suggestion_request.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

BedSuggestionRequest _$BedSuggestionRequestFromJson(
  Map<String, dynamic> json,
) => BedSuggestionRequest(
  admissionId: json['admission_id'] as String?,
  patientIdentifier: json['patient_identifier'] as String?,
);

Map<String, dynamic> _$BedSuggestionRequestToJson(
  BedSuggestionRequest instance,
) => <String, dynamic>{
  'admission_id': instance.admissionId,
  'patient_identifier': instance.patientIdentifier,
};
