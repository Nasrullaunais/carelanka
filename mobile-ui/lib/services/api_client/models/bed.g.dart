// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'bed.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

Bed _$BedFromJson(Map<String, dynamic> json) => Bed(
  id: json['id'] as String,
  wardId: json['ward_id'] as String,
  wardName: json['ward_name'] as String,
  bedNumber: json['bed_number'] as String,
  hasIsolation: json['has_isolation'] as bool,
  nurseStationDistance: (json['nurse_station_distance'] as num).toInt(),
  condition: BedCondition.fromJson(json['condition'] as String),
  createdAt: DateTime.parse(json['created_at'] as String),
  updatedAt: DateTime.parse(json['updated_at'] as String),
  assetTag: json['asset_tag'] as String?,
);

Map<String, dynamic> _$BedToJson(Bed instance) => <String, dynamic>{
  'id': instance.id,
  'ward_id': instance.wardId,
  'ward_name': instance.wardName,
  'bed_number': instance.bedNumber,
  'has_isolation': instance.hasIsolation,
  'nurse_station_distance': instance.nurseStationDistance,
  'condition': instance.condition,
  'asset_tag': instance.assetTag,
  'created_at': instance.createdAt.toIso8601String(),
  'updated_at': instance.updatedAt.toIso8601String(),
};
