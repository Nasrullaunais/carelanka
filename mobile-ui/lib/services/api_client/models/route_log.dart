// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'route_log.g.dart';

@JsonSerializable()
class RouteLog {
  const RouteLog({
    this.dispatchId,
    this.originLatitude,
    this.originLongitude,
    this.destinationLatitude,
    this.destinationLongitude,
    this.plannedDistanceKm,
    this.plannedDurationMinutes,
    this.departedAt,
    this.arrivedAt,
    this.mapsApiReference,
  });
  
  factory RouteLog.fromJson(Map<String, Object?> json) => _$RouteLogFromJson(json);
  
  @JsonKey(name: 'dispatch_id')
  final String? dispatchId;
  @JsonKey(name: 'origin_latitude')
  final double? originLatitude;
  @JsonKey(name: 'origin_longitude')
  final double? originLongitude;
  @JsonKey(name: 'destination_latitude')
  final double? destinationLatitude;
  @JsonKey(name: 'destination_longitude')
  final double? destinationLongitude;
  @JsonKey(name: 'planned_distance_km')
  final double? plannedDistanceKm;
  @JsonKey(name: 'planned_duration_minutes')
  final int? plannedDurationMinutes;
  @JsonKey(name: 'departed_at')
  final DateTime? departedAt;
  @JsonKey(name: 'arrived_at')
  final DateTime? arrivedAt;
  @JsonKey(name: 'maps_api_reference')
  final String? mapsApiReference;

  Map<String, Object?> toJson() => _$RouteLogToJson(this);
}
