// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'care_recommendation_status.dart';

part 'my_care_recommendation.g.dart';

@JsonSerializable()
class MyCareRecommendation {
  const MyCareRecommendation({
    this.id,
    this.reportedText,
    this.reportedAt,
    this.status,
    this.doctorMessage,
  });
  
  factory MyCareRecommendation.fromJson(Map<String, Object?> json) => _$MyCareRecommendationFromJson(json);
  
  final String? id;
  @JsonKey(name: 'reported_text')
  final String? reportedText;
  @JsonKey(name: 'reported_at')
  final DateTime? reportedAt;
  final CareRecommendationStatus? status;
  @JsonKey(name: 'doctor_message')
  final String? doctorMessage;

  Map<String, Object?> toJson() => _$MyCareRecommendationToJson(this);
}
