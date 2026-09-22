// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'navigation_target.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

NavigationTarget _$NavigationTargetFromJson(Map<String, dynamic> json) =>
    NavigationTarget(
      dispatchId: json['dispatch_id'] as String?,
      waypointType: json['waypoint_type'] == null
          ? null
          : NavigationWaypoint.fromJson(json['waypoint_type'] as String),
      destinationLatitude: (json['destination_latitude'] as num?)?.toDouble(),
      destinationLongitude: (json['destination_longitude'] as num?)?.toDouble(),
      destinationLabel: json['destination_label'] as String?,
      googleMapsUrl: json['google_maps_url'] as String?,
    );

Map<String, dynamic> _$NavigationTargetToJson(NavigationTarget instance) =>
    <String, dynamic>{
      'dispatch_id': instance.dispatchId,
      'waypoint_type': instance.waypointType,
      'destination_latitude': instance.destinationLatitude,
      'destination_longitude': instance.destinationLongitude,
      'destination_label': instance.destinationLabel,
      'google_maps_url': instance.googleMapsUrl,
    };
