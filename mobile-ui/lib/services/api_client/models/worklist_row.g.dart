// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'worklist_row.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

WorklistRow _$WorklistRowFromJson(Map<String, dynamic> json) => WorklistRow(
  id: json['id'] as String,
  kind: WorklistKind.fromJson(json['kind'] as String),
  patient: PatientSummary.fromJson(json['patient'] as Map<String, dynamic>),
  status: WorklistStatus.fromJson(json['status'] as String),
  requiresBed: json['requires_bed'] as bool,
  whenValue: DateTime.parse(json['when'] as String),
  source: json['source'] == null
      ? null
      : AdmissionSource.fromJson(json['source'] as String),
  admissionCategory: json['admission_category'] == null
      ? null
      : AdmissionCategory.fromJson(json['admission_category'] as String),
  urgency: json['urgency'] == null
      ? null
      : AdmissionUrgency.fromJson(json['urgency'] as String),
  wardName: json['ward_name'] as String?,
  bedNumber: json['bed_number'] as String?,
  reason: json['reason'] as String?,
);

Map<String, dynamic> _$WorklistRowToJson(WorklistRow instance) =>
    <String, dynamic>{
      'id': instance.id,
      'kind': instance.kind,
      'patient': instance.patient,
      'status': instance.status,
      'requires_bed': instance.requiresBed,
      'source': instance.source,
      'admission_category': instance.admissionCategory,
      'urgency': instance.urgency,
      'ward_name': instance.wardName,
      'bed_number': instance.bedNumber,
      'when': instance.whenValue.toIso8601String(),
      'reason': instance.reason,
    };
