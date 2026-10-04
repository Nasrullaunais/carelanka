// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'my_call_tracking.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

MyCallTracking _$MyCallTrackingFromJson(Map<String, dynamic> json) =>
    MyCallTracking(
      emergencyCallId: json['emergency_call_id'] as String?,
      callStatus: json['call_status'] == null
          ? null
          : CallStatus.fromJson(json['call_status'] as String),
      dispatchStatus: json['dispatch_status'] == null
          ? null
          : DispatchStatus.fromJson(json['dispatch_status'] as String),
      ambulanceIsOnTheWay: json['ambulance_is_on_the_way'] as bool?,
      lookingForAnotherAmbulance:
          json['looking_for_another_ambulance'] as bool?,
      ambulanceRegistration: json['ambulance_registration'] as String?,
      ambulanceLatitude: (json['ambulance_latitude'] as num?)?.toDouble(),
      ambulanceLongitude: (json['ambulance_longitude'] as num?)?.toDouble(),
      ambulanceLocationIsStale: json['ambulance_location_is_stale'] as bool?,
      estimatedMinutesToArrival: (json['estimated_minutes_to_arrival'] as num?)
          ?.toInt(),
      ambulanceDistanceKm: (json['ambulance_distance_km'] as num?)?.toDouble(),
      cancellationRequestStatus: json['cancellation_request_status'] == null
          ? null
          : CancellationRequestStatus.fromJson(
              json['cancellation_request_status'] as String,
            ),
      cancellationReviewNotes: json['cancellation_review_notes'] as String?,
      outcome: json['outcome'] == null
          ? null
          : EmergencyCallOutcome.fromJson(json['outcome'] as String),
      updatedAt: json['updated_at'] == null
          ? null
          : DateTime.parse(json['updated_at'] as String),
    );

Map<String, dynamic> _$MyCallTrackingToJson(MyCallTracking instance) =>
    <String, dynamic>{
      'emergency_call_id': instance.emergencyCallId,
      'call_status': instance.callStatus,
      'dispatch_status': instance.dispatchStatus,
      'ambulance_is_on_the_way': instance.ambulanceIsOnTheWay,
      'looking_for_another_ambulance': instance.lookingForAnotherAmbulance,
      'ambulance_registration': instance.ambulanceRegistration,
      'ambulance_latitude': instance.ambulanceLatitude,
      'ambulance_longitude': instance.ambulanceLongitude,
      'ambulance_location_is_stale': instance.ambulanceLocationIsStale,
      'estimated_minutes_to_arrival': instance.estimatedMinutesToArrival,
      'ambulance_distance_km': instance.ambulanceDistanceKm,
      'cancellation_request_status': instance.cancellationRequestStatus,
      'cancellation_review_notes': instance.cancellationReviewNotes,
      'outcome': instance.outcome,
      'updated_at': instance.updatedAt?.toIso8601String(),
    };
