// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'update_medical_profile_request.g.dart';

@JsonSerializable()
class UpdateMedicalProfileRequest {
  const UpdateMedicalProfileRequest({
    this.knownConditions,
    this.allergies,
    this.currentSymptoms,
    this.recentSituation,
  });
  
  factory UpdateMedicalProfileRequest.fromJson(Map<String, Object?> json) => _$UpdateMedicalProfileRequestFromJson(json);
  
  @JsonKey(name: 'known_conditions')
  final String? knownConditions;
  final String? allergies;
  @JsonKey(name: 'current_symptoms')
  final String? currentSymptoms;
  @JsonKey(name: 'recent_situation')
  final String? recentSituation;

  Map<String, Object?> toJson() => _$UpdateMedicalProfileRequestToJson(this);
}
