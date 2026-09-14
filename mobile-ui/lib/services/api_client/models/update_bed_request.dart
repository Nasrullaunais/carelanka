// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'bed_condition.dart';

part 'update_bed_request.g.dart';

@JsonSerializable()
class UpdateBedRequest {
  const UpdateBedRequest({
    this.hasIsolation,
    this.nurseStationDistance,
    this.condition,
    this.assetTag,
  });
  
  factory UpdateBedRequest.fromJson(Map<String, Object?> json) => _$UpdateBedRequestFromJson(json);
  
  @JsonKey(name: 'has_isolation')
  final bool? hasIsolation;
  @JsonKey(name: 'nurse_station_distance')
  final int? nurseStationDistance;
  final BedCondition? condition;
  @JsonKey(name: 'asset_tag')
  final String? assetTag;

  Map<String, Object?> toJson() => _$UpdateBedRequestToJson(this);
}
