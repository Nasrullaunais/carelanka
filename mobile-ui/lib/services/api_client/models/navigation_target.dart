// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'navigation_waypoint.dart';

part 'navigation_target.g.dart';

@JsonSerializable()
class NavigationTarget {
  const NavigationTarget({
    this.dispatchId,
    this.waypointType,
    this.destinationLatitude,
    this.destinationLongitude,
    this.destinationLabel,
    this.googleMapsUrl,
  });
  
  factory NavigationTarget.fromJson(Map<String, Object?> json) => _$NavigationTargetFromJson(json);
  
  @JsonKey(name: 'dispatch_id')
  final String? dispatchId;
  @JsonKey(name: 'waypoint_type')
  final NavigationWaypoint? waypointType;
  @JsonKey(name: 'destination_latitude')
  final double? destinationLatitude;
  @JsonKey(name: 'destination_longitude')
  final double? destinationLongitude;
  @JsonKey(name: 'destination_label')
  final String? destinationLabel;
  @JsonKey(name: 'google_maps_url')
  final String? googleMapsUrl;

  Map<String, Object?> toJson() => _$NavigationTargetToJson(this);
}
