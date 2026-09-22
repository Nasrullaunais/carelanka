// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'bed_suggestion_request.g.dart';

@JsonSerializable()
class BedSuggestionRequest {
  const BedSuggestionRequest({
    this.admissionId,
    this.patientIdentifier,
  });
  
  factory BedSuggestionRequest.fromJson(Map<String, Object?> json) => _$BedSuggestionRequestFromJson(json);
  
  @JsonKey(name: 'admission_id')
  final String? admissionId;
  @JsonKey(name: 'patient_identifier')
  final String? patientIdentifier;

  Map<String, Object?> toJson() => _$BedSuggestionRequestToJson(this);
}
