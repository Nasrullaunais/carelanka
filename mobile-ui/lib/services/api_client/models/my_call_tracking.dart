// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'call_status.dart';
import 'cancellation_request_status.dart';
import 'dispatch_status.dart';
import 'emergency_call_outcome.dart';

part 'my_call_tracking.g.dart';

@JsonSerializable()
class MyCallTracking {
  const MyCallTracking({
    this.emergencyCallId,
    this.callStatus,
    this.dispatchStatus,
    this.ambulanceIsOnTheWay,
    this.lookingForAnotherAmbulance,
    this.ambulanceRegistration,
    this.ambulanceLatitude,
    this.ambulanceLongitude,
    this.ambulanceLocationIsStale,
    this.estimatedMinutesToArrival,
    this.ambulanceDistanceKm,
    this.cancellationRequestStatus,
    this.cancellationReviewNotes,
    this.outcome,
    this.updatedAt,
  });
  
  factory MyCallTracking.fromJson(Map<String, Object?> json) => _$MyCallTrackingFromJson(json);
  
  @JsonKey(name: 'emergency_call_id')
  final String? emergencyCallId;
  @JsonKey(name: 'call_status')
  final CallStatus? callStatus;
  @JsonKey(name: 'dispatch_status')
  final DispatchStatus? dispatchStatus;
  @JsonKey(name: 'ambulance_is_on_the_way')
  final bool? ambulanceIsOnTheWay;
  @JsonKey(name: 'looking_for_another_ambulance')
  final bool? lookingForAnotherAmbulance;
  @JsonKey(name: 'ambulance_registration')
  final String? ambulanceRegistration;
  @JsonKey(name: 'ambulance_latitude')
  final double? ambulanceLatitude;
  @JsonKey(name: 'ambulance_longitude')
  final double? ambulanceLongitude;
  @JsonKey(name: 'ambulance_location_is_stale')
  final bool? ambulanceLocationIsStale;
  @JsonKey(name: 'estimated_minutes_to_arrival')
  final int? estimatedMinutesToArrival;
  @JsonKey(name: 'ambulance_distance_km')
  final double? ambulanceDistanceKm;
  @JsonKey(name: 'cancellation_request_status')
  final CancellationRequestStatus? cancellationRequestStatus;
  @JsonKey(name: 'cancellation_review_notes')
  final String? cancellationReviewNotes;
  final EmergencyCallOutcome? outcome;
  @JsonKey(name: 'updated_at')
  final DateTime? updatedAt;

  Map<String, Object?> toJson() => _$MyCallTrackingToJson(this);
}
