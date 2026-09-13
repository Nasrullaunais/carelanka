// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'discharge_candidate.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

DischargeCandidate _$DischargeCandidateFromJson(Map<String, dynamic> json) =>
    DischargeCandidate(
      admissionId: json['admission_id'] as String,
      patient: PatientSummary.fromJson(json['patient'] as Map<String, dynamic>),
      wardName: json['ward_name'] as String,
      bedNumber: json['bed_number'] as String,
      admissionCategory: AdmissionCategory.fromJson(
        json['admission_category'] as String,
      ),
      daysInBed: (json['days_in_bed'] as num).toInt(),
      outstandingItems: (json['outstanding_items'] as List<dynamic>)
          .map((e) => e as String)
          .toList(),
      isDischarged: json['is_discharged'] as bool,
      admittedAt: json['admitted_at'] == null
          ? null
          : DateTime.parse(json['admitted_at'] as String),
      dischargedAt: json['discharged_at'] == null
          ? null
          : DateTime.parse(json['discharged_at'] as String),
    );

Map<String, dynamic> _$DischargeCandidateToJson(DischargeCandidate instance) =>
    <String, dynamic>{
      'admission_id': instance.admissionId,
      'patient': instance.patient,
      'ward_name': instance.wardName,
      'bed_number': instance.bedNumber,
      'admission_category': instance.admissionCategory,
      'admitted_at': instance.admittedAt?.toIso8601String(),
      'days_in_bed': instance.daysInBed,
      'outstanding_items': instance.outstandingItems,
      'is_discharged': instance.isDischarged,
      'discharged_at': instance.dischargedAt?.toIso8601String(),
    };
