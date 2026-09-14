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
      ambulanceIsOnTheWay: json['ambulance_is_on_the_way'] as bool?,
      ambulanceLatitude: (json['ambulance_latitude'] as num?)?.toDouble(),
      ambulanceLongitude: (json['ambulance_longitude'] as num?)?.toDouble(),
      ambulanceLocationIsStale: json['ambulance_location_is_stale'] as bool?,
      estimatedMinutesToArrival: (json['estimated_minutes_to_arrival'] as num?)
          ?.toInt(),
      cancellationRequestStatus: json['cancellation_request_status'] == null
          ? null
          : CancellationRequestStatus.fromJson(
              json['cancellation_request_status'] as String,
            ),
      updatedAt: json['updated_at'] == null
          ? null
          : DateTime.parse(json['updated_at'] as String),
    );

Map<String, dynamic> _$MyCallTrackingToJson(MyCallTracking instance) =>
    <String, dynamic>{
      'emergency_call_id': instance.emergencyCallId,
      'call_status': instance.callStatus,
      'ambulance_is_on_the_way': instance.ambulanceIsOnTheWay,
      'ambulance_latitude': instance.ambulanceLatitude,
      'ambulance_longitude': instance.ambulanceLongitude,
      'ambulance_location_is_stale': instance.ambulanceLocationIsStale,
      'estimated_minutes_to_arrival': instance.estimatedMinutesToArrival,
      'cancellation_request_status': instance.cancellationRequestStatus,
      'updated_at': instance.updatedAt?.toIso8601String(),
    };
