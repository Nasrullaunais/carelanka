// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'ambulance_summary.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

AmbulanceSummary _$AmbulanceSummaryFromJson(
  Map<String, dynamic> json,
) => AmbulanceSummary(
  id: json['id'] as String,
  registrationNumber: json['registration_number'] as String,
  status: AmbulanceStatus.fromJson(json['status'] as String),
  isDivertible: json['is_divertible'] as bool,
  currentLatitude: (json['current_latitude'] as num?)?.toDouble(),
  currentLongitude: (json['current_longitude'] as num?)?.toDouble(),
  locationUpdatedAt: json['location_updated_at'] == null
      ? null
      : DateTime.parse(json['location_updated_at'] as String),
  currentCrewCount: (json['current_crew_count'] as num?)?.toInt(),
  requiredCrewCount: (json['required_crew_count'] as num?)?.toInt(),
  isEligible: json['is_eligible'] as bool?,
  eligibilityBlockReasons: (json['eligibility_block_reasons'] as List<dynamic>?)
      ?.map((e) => AmbulanceEligibilityBlockReason.fromJson(e as String))
      .toList(),
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
      'location_updated_at': instance.locationUpdatedAt?.toIso8601String(),
      'current_crew_count': instance.currentCrewCount,
      'required_crew_count': instance.requiredCrewCount,
      'is_eligible': instance.isEligible,
      'eligibility_block_reasons': instance.eligibilityBlockReasons,
      'active_dispatch_id': instance.activeDispatchId,
      'is_divertible': instance.isDivertible,
      'distance_km': instance.distanceKm,
    };
