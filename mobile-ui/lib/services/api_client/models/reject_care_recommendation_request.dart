// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'reject_care_recommendation_request.g.dart';

@JsonSerializable()
class RejectCareRecommendationRequest {
  const RejectCareRecommendationRequest({
    this.reason,
  });
  
  factory RejectCareRecommendationRequest.fromJson(Map<String, Object?> json) => _$RejectCareRecommendationRequestFromJson(json);
  
  final String? reason;

  Map<String, Object?> toJson() => _$RejectCareRecommendationRequestToJson(this);
}
