// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'route_log.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

RouteLog _$RouteLogFromJson(Map<String, dynamic> json) => RouteLog(
  dispatchId: json['dispatch_id'] as String?,
  originLatitude: (json['origin_latitude'] as num?)?.toDouble(),
  originLongitude: (json['origin_longitude'] as num?)?.toDouble(),
  destinationLatitude: (json['destination_latitude'] as num?)?.toDouble(),
  destinationLongitude: (json['destination_longitude'] as num?)?.toDouble(),
  plannedDistanceKm: (json['planned_distance_km'] as num?)?.toDouble(),
  plannedDurationMinutes: (json['planned_duration_minutes'] as num?)?.toInt(),
  departedAt: json['departed_at'] == null
      ? null
      : DateTime.parse(json['departed_at'] as String),
  arrivedAt: json['arrived_at'] == null
      ? null
      : DateTime.parse(json['arrived_at'] as String),
  mapsApiReference: json['maps_api_reference'] as String?,
);

Map<String, dynamic> _$RouteLogToJson(RouteLog instance) => <String, dynamic>{
  'dispatch_id': instance.dispatchId,
  'origin_latitude': instance.originLatitude,
  'origin_longitude': instance.originLongitude,
  'destination_latitude': instance.destinationLatitude,
  'destination_longitude': instance.destinationLongitude,
  'planned_distance_km': instance.plannedDistanceKm,
  'planned_duration_minutes': instance.plannedDurationMinutes,
  'departed_at': instance.departedAt?.toIso8601String(),
  'arrived_at': instance.arrivedAt?.toIso8601String(),
  'maps_api_reference': instance.mapsApiReference,
};
