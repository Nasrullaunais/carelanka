// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'call_status.dart';
import 'cancellation_request_status.dart';

part 'my_call_tracking.g.dart';

@JsonSerializable()
class MyCallTracking {
  const MyCallTracking({
    this.emergencyCallId,
    this.callStatus,
    this.ambulanceIsOnTheWay,
    this.ambulanceLatitude,
    this.ambulanceLongitude,
    this.ambulanceLocationIsStale,
    this.estimatedMinutesToArrival,
    this.cancellationRequestStatus,
    this.updatedAt,
  });

  factory MyCallTracking.fromJson(Map<String, Object?> json) => _$MyCallTrackingFromJson(json);

  @JsonKey(name: 'emergency_call_id')
  final String? emergencyCallId;
  @JsonKey(name: 'call_status')
  final CallStatus? callStatus;
  @JsonKey(name: 'ambulance_is_on_the_way')
  final bool? ambulanceIsOnTheWay;
  @JsonKey(name: 'ambulance_latitude')
  final double? ambulanceLatitude;
  @JsonKey(name: 'ambulance_longitude')
  final double? ambulanceLongitude;
  @JsonKey(name: 'ambulance_location_is_stale')
  final bool? ambulanceLocationIsStale;
  @JsonKey(name: 'estimated_minutes_to_arrival')
  final int? estimatedMinutesToArrival;
  @JsonKey(name: 'cancellation_request_status')
  final CancellationRequestStatus? cancellationRequestStatus;
  @JsonKey(name: 'updated_at')
  final DateTime? updatedAt;

  Map<String, Object?> toJson() => _$MyCallTrackingToJson(this);
}
