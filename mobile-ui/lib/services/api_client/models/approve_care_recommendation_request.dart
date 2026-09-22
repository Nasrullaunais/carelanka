// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'approve_care_recommendation_request.g.dart';

@JsonSerializable()
class ApproveCareRecommendationRequest {
  const ApproveCareRecommendationRequest({
    this.doctorMessage,
  });
  
  factory ApproveCareRecommendationRequest.fromJson(Map<String, Object?> json) => _$ApproveCareRecommendationRequestFromJson(json);
  
  @JsonKey(name: 'doctor_message')
  final String? doctorMessage;

  Map<String, Object?> toJson() => _$ApproveCareRecommendationRequestToJson(this);
}
