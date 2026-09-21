// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'care_recommendation_status.dart';
import 'care_urgency.dart';

part 'care_recommendation_summary.g.dart';

@JsonSerializable()
class CareRecommendationSummary {
  const CareRecommendationSummary({
    this.id,
    this.patientId,
    this.reportedAt,
    this.redFlag,
    this.urgencyFlag,
    this.status,
  });
  
  factory CareRecommendationSummary.fromJson(Map<String, Object?> json) => _$CareRecommendationSummaryFromJson(json);
  
  final String? id;
  @JsonKey(name: 'patient_id')
  final String? patientId;
  @JsonKey(name: 'reported_at')
  final DateTime? reportedAt;
  @JsonKey(name: 'red_flag')
  final bool? redFlag;
  @JsonKey(name: 'urgency_flag')
  final CareUrgency? urgencyFlag;
  final CareRecommendationStatus? status;

  Map<String, Object?> toJson() => _$CareRecommendationSummaryToJson(this);
}
