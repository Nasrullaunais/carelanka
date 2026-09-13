// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'update_bed_request.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

UpdateBedRequest _$UpdateBedRequestFromJson(Map<String, dynamic> json) =>
    UpdateBedRequest(
      hasIsolation: json['has_isolation'] as bool?,
      nurseStationDistance: (json['nurse_station_distance'] as num?)?.toInt(),
      condition: json['condition'] == null
          ? null
          : BedCondition.fromJson(json['condition'] as String),
      assetTag: json['asset_tag'] as String?,
    );

Map<String, dynamic> _$UpdateBedRequestToJson(UpdateBedRequest instance) =>
    <String, dynamic>{
      'has_isolation': instance.hasIsolation,
      'nurse_station_distance': instance.nurseStationDistance,
      'condition': instance.condition,
      'asset_tag': instance.assetTag,
    };
