// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'admission_bed.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

AdmissionBed _$AdmissionBedFromJson(Map<String, dynamic> json) => AdmissionBed(
  id: json['id'] as String,
  wardId: json['ward_id'] as String,
  wardName: json['ward_name'] as String,
  bedNumber: json['bed_number'] as String,
  hasIsolation: json['has_isolation'] as bool,
  condition: BedCondition.fromJson(json['condition'] as String),
  availability: BedAvailability.fromJson(json['availability'] as String),
  occupiedByAdmissionId: json['occupied_by_admission_id'] as String?,
  createdAt: json['created_at'] == null
      ? null
      : DateTime.parse(json['created_at'] as String),
  updatedAt: json['updated_at'] == null
      ? null
      : DateTime.parse(json['updated_at'] as String),
);

Map<String, dynamic> _$AdmissionBedToJson(AdmissionBed instance) =>
    <String, dynamic>{
      'id': instance.id,
      'ward_id': instance.wardId,
      'ward_name': instance.wardName,
      'bed_number': instance.bedNumber,
      'has_isolation': instance.hasIsolation,
      'condition': instance.condition,
      'availability': instance.availability,
      'occupied_by_admission_id': instance.occupiedByAdmissionId,
      'created_at': instance.createdAt?.toIso8601String(),
      'updated_at': instance.updatedAt?.toIso8601String(),
    };
