// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'create_bed_request.g.dart';

@JsonSerializable()
class CreateBedRequest {
  const CreateBedRequest({
    required this.wardId,
    required this.bedNumber,
    this.hasIsolation,
    this.nurseStationDistance,
    this.assetTag,
  });
  
  factory CreateBedRequest.fromJson(Map<String, Object?> json) => _$CreateBedRequestFromJson(json);
  
  @JsonKey(name: 'ward_id')
  final String wardId;
  @JsonKey(name: 'bed_number')
  final String bedNumber;
  @JsonKey(name: 'has_isolation')
  final bool? hasIsolation;
  @JsonKey(name: 'nurse_station_distance')
  final int? nurseStationDistance;
  @JsonKey(name: 'asset_tag')
  final String? assetTag;

  Map<String, Object?> toJson() => _$CreateBedRequestToJson(this);
}
