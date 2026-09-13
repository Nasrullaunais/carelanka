// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'discharge.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

Discharge _$DischargeFromJson(Map<String, dynamic> json) => Discharge(
  id: json['id'] as String,
  admissionId: json['admission_id'] as String,
  flaggedBy: AssignedBy.fromJson(json['flagged_by'] as String),
  flaggedAt: DateTime.parse(json['flagged_at'] as String),
  checklist: (json['checklist'] as Map<String, dynamic>).map(
    (k, e) => MapEntry(k, ChecklistItem.fromJson(e as Map<String, dynamic>)),
  ),
  allMandatoryTicked: json['all_mandatory_ticked'] as bool,
  createdAt: DateTime.parse(json['created_at'] as String),
  updatedAt: DateTime.parse(json['updated_at'] as String),
  confirmedByStaffId: json['confirmed_by_staff_id'] as String?,
  confirmedByStaffName: json['confirmed_by_staff_name'] as String?,
  confirmedAt: json['confirmed_at'] == null
      ? null
      : DateTime.parse(json['confirmed_at'] as String),
  summaryNote: json['summary_note'] as String?,
);

Map<String, dynamic> _$DischargeToJson(Discharge instance) => <String, dynamic>{
  'id': instance.id,
  'admission_id': instance.admissionId,
  'flagged_by': instance.flaggedBy,
  'flagged_at': instance.flaggedAt.toIso8601String(),
  'checklist': instance.checklist,
  'all_mandatory_ticked': instance.allMandatoryTicked,
  'confirmed_by_staff_id': instance.confirmedByStaffId,
  'confirmed_by_staff_name': instance.confirmedByStaffName,
  'confirmed_at': instance.confirmedAt?.toIso8601String(),
  'summary_note': instance.summaryNote,
  'created_at': instance.createdAt.toIso8601String(),
  'updated_at': instance.updatedAt.toIso8601String(),
};
