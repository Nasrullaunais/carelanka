// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'bed_condition.dart';

part 'bed.g.dart';

@JsonSerializable()
class Bed {
  const Bed({
    required this.id,
    required this.wardId,
    required this.wardName,
    required this.bedNumber,
    required this.hasIsolation,
    required this.nurseStationDistance,
    required this.condition,
    required this.createdAt,
    required this.updatedAt,
    this.assetTag,
  });
  
  factory Bed.fromJson(Map<String, Object?> json) => _$BedFromJson(json);
  
  final String id;
  @JsonKey(name: 'ward_id')
  final String wardId;
  @JsonKey(name: 'ward_name')
  final String wardName;
  @JsonKey(name: 'bed_number')
  final String bedNumber;
  @JsonKey(name: 'has_isolation')
  final bool hasIsolation;
  @JsonKey(name: 'nurse_station_distance')
  final int nurseStationDistance;
  final BedCondition condition;
  @JsonKey(name: 'asset_tag')
  final String? assetTag;
  @JsonKey(name: 'created_at')
  final DateTime createdAt;
  @JsonKey(name: 'updated_at')
  final DateTime updatedAt;

  Map<String, Object?> toJson() => _$BedToJson(this);
}
