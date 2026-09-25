// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'admission_summary.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

AdmissionSummary _$AdmissionSummaryFromJson(Map<String, dynamic> json) =>
    AdmissionSummary(
      id: json['id'] as String,
      source: AdmissionSource.fromJson(json['source'] as String),
      urgency: AdmissionUrgency.fromJson(json['urgency'] as String),
      status: AdmissionStatus.fromJson(json['status'] as String),
      detailsComplete: json['details_complete'] as bool,
      patient: json['patient'] == null
          ? null
          : PatientSummary.fromJson(json['patient'] as Map<String, dynamic>),
      admissionCategory: json['admission_category'] == null
          ? null
          : AdmissionCategory.fromJson(json['admission_category'] as String),
      wardName: json['ward_name'] as String?,
      bedNumber: json['bed_number'] as String?,
      expectedArrival: json['expected_arrival'] == null
          ? null
          : DateTime.parse(json['expected_arrival'] as String),
      admittedAt: json['admitted_at'] == null
          ? null
          : DateTime.parse(json['admitted_at'] as String),
    );

Map<String, dynamic> _$AdmissionSummaryToJson(AdmissionSummary instance) =>
    <String, dynamic>{
      'id': instance.id,
      'patient': instance.patient,
      'source': instance.source,
      'admission_category': instance.admissionCategory,
      'urgency': instance.urgency,
      'status': instance.status,
      'details_complete': instance.detailsComplete,
      'ward_name': instance.wardName,
      'bed_number': instance.bedNumber,
      'expected_arrival': instance.expectedArrival?.toIso8601String(),
      'admitted_at': instance.admittedAt?.toIso8601String(),
    };
