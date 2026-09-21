// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'care_recommendation_status.dart';
import 'care_urgency.dart';

part 'care_recommendation.g.dart';

@JsonSerializable()
class CareRecommendation {
  const CareRecommendation({
    this.id,
    this.patientId,
    this.admissionId,
    this.reportedText,
    this.reportedAt,
    this.redFlag,
    this.urgencyFlag,
    this.agentMessage,
    this.status,
    this.workflowId,
    this.reviewedByStaffId,
    this.reviewedAt,
    this.doctorMessage,
    this.rejectionReason,
    this.createdAt,
    this.updatedAt,
  });
  
  factory CareRecommendation.fromJson(Map<String, Object?> json) => _$CareRecommendationFromJson(json);
  
  final String? id;
  @JsonKey(name: 'patient_id')
  final String? patientId;
  @JsonKey(name: 'admission_id')
  final String? admissionId;
  @JsonKey(name: 'reported_text')
  final String? reportedText;
  @JsonKey(name: 'reported_at')
  final DateTime? reportedAt;
  @JsonKey(name: 'red_flag')
  final bool? redFlag;
  @JsonKey(name: 'urgency_flag')
  final CareUrgency? urgencyFlag;
  @JsonKey(name: 'agent_message')
  final String? agentMessage;
  final CareRecommendationStatus? status;
  @JsonKey(name: 'workflow_id')
  final String? workflowId;
  @JsonKey(name: 'reviewed_by_staff_id')
  final String? reviewedByStaffId;
  @JsonKey(name: 'reviewed_at')
  final DateTime? reviewedAt;
  @JsonKey(name: 'doctor_message')
  final String? doctorMessage;
  @JsonKey(name: 'rejection_reason')
  final String? rejectionReason;
  @JsonKey(name: 'created_at')
  final DateTime? createdAt;
  @JsonKey(name: 'updated_at')
  final DateTime? updatedAt;

  Map<String, Object?> toJson() => _$CareRecommendationToJson(this);
}
