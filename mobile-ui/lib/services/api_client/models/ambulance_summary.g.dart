// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'ambulance_summary.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

AmbulanceSummary _$AmbulanceSummaryFromJson(Map<String, dynamic> json) =>
    AmbulanceSummary(
      id: json['id'] as String,
      registrationNumber: json['registration_number'] as String,
      status: AmbulanceStatus.fromJson(json['status'] as String),
      isDivertible: json['is_divertible'] as bool,
      currentLatitude: (json['current_latitude'] as num?)?.toDouble(),
      currentLongitude: (json['current_longitude'] as num?)?.toDouble(),
      activeDispatchId: json['active_dispatch_id'] as String?,
      distanceKm: (json['distance_km'] as num?)?.toDouble(),
    );

Map<String, dynamic> _$AmbulanceSummaryToJson(AmbulanceSummary instance) =>
    <String, dynamic>{
      'id': instance.id,
      'registration_number': instance.registrationNumber,
      'status': instance.status,
      'current_latitude': instance.currentLatitude,
      'current_longitude': instance.currentLongitude,
      'active_dispatch_id': instance.activeDispatchId,
      'is_divertible': instance.isDivertible,
      'distance_km': instance.distanceKm,
    };
