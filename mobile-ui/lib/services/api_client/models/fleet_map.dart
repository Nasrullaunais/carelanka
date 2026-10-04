// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'fleet_map_ambulance.dart';
import 'fleet_map_call.dart';

part 'fleet_map.g.dart';

@JsonSerializable()
class FleetMap {
  const FleetMap({
    this.ambulances,
    this.calls,
    this.locationMaxAgeMinutes,
    this.generatedAt,
  });
  
  factory FleetMap.fromJson(Map<String, Object?> json) => _$FleetMapFromJson(json);
  
  final List<FleetMapAmbulance>? ambulances;
  final List<FleetMapCall>? calls;
  @JsonKey(name: 'location_max_age_minutes')
  final int? locationMaxAgeMinutes;
  @JsonKey(name: 'generated_at')
  final DateTime? generatedAt;

  Map<String, Object?> toJson() => _$FleetMapToJson(this);
}
