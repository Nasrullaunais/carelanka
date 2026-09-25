// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'care_recommendation.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

CareRecommendation _$CareRecommendationFromJson(Map<String, dynamic> json) =>
    CareRecommendation(
      id: json['id'] as String?,
      patientId: json['patient_id'] as String?,
      admissionId: json['admission_id'] as String?,
      reportedText: json['reported_text'] as String?,
      reportedAt: json['reported_at'] == null
          ? null
          : DateTime.parse(json['reported_at'] as String),
      redFlag: json['red_flag'] as bool?,
      urgencyFlag: json['urgency_flag'] == null
          ? null
          : CareUrgency.fromJson(json['urgency_flag'] as String),
      agentMessage: json['agent_message'] as String?,
      status: json['status'] == null
          ? null
          : CareRecommendationStatus.fromJson(json['status'] as String),
      workflowId: json['workflow_id'] as String?,
      reviewedByStaffId: json['reviewed_by_staff_id'] as String?,
      reviewedAt: json['reviewed_at'] == null
          ? null
          : DateTime.parse(json['reviewed_at'] as String),
      doctorMessage: json['doctor_message'] as String?,
      rejectionReason: json['rejection_reason'] as String?,
      createdAt: json['created_at'] == null
          ? null
          : DateTime.parse(json['created_at'] as String),
      updatedAt: json['updated_at'] == null
          ? null
          : DateTime.parse(json['updated_at'] as String),
    );

Map<String, dynamic> _$CareRecommendationToJson(CareRecommendation instance) =>
    <String, dynamic>{
      'id': instance.id,
      'patient_id': instance.patientId,
      'admission_id': instance.admissionId,
      'reported_text': instance.reportedText,
      'reported_at': instance.reportedAt?.toIso8601String(),
      'red_flag': instance.redFlag,
      'urgency_flag': instance.urgencyFlag,
      'agent_message': instance.agentMessage,
      'status': instance.status,
      'workflow_id': instance.workflowId,
      'reviewed_by_staff_id': instance.reviewedByStaffId,
      'reviewed_at': instance.reviewedAt?.toIso8601String(),
      'doctor_message': instance.doctorMessage,
      'rejection_reason': instance.rejectionReason,
      'created_at': instance.createdAt?.toIso8601String(),
      'updated_at': instance.updatedAt?.toIso8601String(),
    };
