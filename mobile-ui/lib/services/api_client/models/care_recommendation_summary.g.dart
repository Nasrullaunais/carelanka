// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'care_recommendation_summary.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

CareRecommendationSummary _$CareRecommendationSummaryFromJson(
  Map<String, dynamic> json,
) => CareRecommendationSummary(
  id: json['id'] as String?,
  patientId: json['patient_id'] as String?,
  reportedAt: json['reported_at'] == null
      ? null
      : DateTime.parse(json['reported_at'] as String),
  redFlag: json['red_flag'] as bool?,
  urgencyFlag: json['urgency_flag'] == null
      ? null
      : CareUrgency.fromJson(json['urgency_flag'] as String),
  status: json['status'] == null
      ? null
      : CareRecommendationStatus.fromJson(json['status'] as String),
);

Map<String, dynamic> _$CareRecommendationSummaryToJson(
  CareRecommendationSummary instance,
) => <String, dynamic>{
  'id': instance.id,
  'patient_id': instance.patientId,
  'reported_at': instance.reportedAt?.toIso8601String(),
  'red_flag': instance.redFlag,
  'urgency_flag': instance.urgencyFlag,
  'status': instance.status,
};
