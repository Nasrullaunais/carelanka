// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'care_recommendation_status.dart';
import 'care_reviewer_role.dart';

part 'my_care_recommendation.g.dart';

@JsonSerializable()
class MyCareRecommendation {
  const MyCareRecommendation({
    this.id,
    this.reportedText,
    this.reportedAt,
    this.status,
    this.doctorMessage,
    this.reviewedByName,
    this.reviewedByRole,
    this.reviewedAt,
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
  @JsonKey(name: 'reviewed_by_name')
  final String? reviewedByName;
  @JsonKey(name: 'reviewed_by_role')
  final CareReviewerRole? reviewedByRole;
  @JsonKey(name: 'reviewed_at')
  final DateTime? reviewedAt;

  Map<String, Object?> toJson() => _$MyCareRecommendationToJson(this);
}
