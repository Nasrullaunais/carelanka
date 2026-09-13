// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'create_bed_request.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

CreateBedRequest _$CreateBedRequestFromJson(Map<String, dynamic> json) =>
    CreateBedRequest(
      wardId: json['ward_id'] as String,
      bedNumber: json['bed_number'] as String,
      hasIsolation: json['has_isolation'] as bool?,
      nurseStationDistance: (json['nurse_station_distance'] as num?)?.toInt(),
      assetTag: json['asset_tag'] as String?,
    );

Map<String, dynamic> _$CreateBedRequestToJson(CreateBedRequest instance) =>
    <String, dynamic>{
      'ward_id': instance.wardId,
      'bed_number': instance.bedNumber,
      'has_isolation': instance.hasIsolation,
      'nurse_station_distance': instance.nurseStationDistance,
      'asset_tag': instance.assetTag,
    };
