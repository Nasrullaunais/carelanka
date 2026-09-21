// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'my_care_recommendation.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

MyCareRecommendation _$MyCareRecommendationFromJson(
  Map<String, dynamic> json,
) => MyCareRecommendation(
  id: json['id'] as String?,
  reportedText: json['reported_text'] as String?,
  reportedAt: json['reported_at'] == null
      ? null
      : DateTime.parse(json['reported_at'] as String),
  status: json['status'] == null
      ? null
      : CareRecommendationStatus.fromJson(json['status'] as String),
  doctorMessage: json['doctor_message'] as String?,
  reviewedByName: json['reviewed_by_name'] as String?,
  reviewedByRole: json['reviewed_by_role'] == null
      ? null
      : CareReviewerRole.fromJson(json['reviewed_by_role'] as String),
  reviewedAt: json['reviewed_at'] == null
      ? null
      : DateTime.parse(json['reviewed_at'] as String),
);

Map<String, dynamic> _$MyCareRecommendationToJson(
  MyCareRecommendation instance,
) => <String, dynamic>{
  'id': instance.id,
  'reported_text': instance.reportedText,
  'reported_at': instance.reportedAt?.toIso8601String(),
  'status': instance.status,
  'doctor_message': instance.doctorMessage,
  'reviewed_by_name': instance.reviewedByName,
  'reviewed_by_role': instance.reviewedByRole,
  'reviewed_at': instance.reviewedAt?.toIso8601String(),
};
