// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'fleet_map_ambulance.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

FleetMapAmbulance _$FleetMapAmbulanceFromJson(
  Map<String, dynamic> json,
) => FleetMapAmbulance(
  id: json['id'] as String?,
  registrationNumber: json['registration_number'] as String?,
  status: json['status'] == null
      ? null
      : AmbulanceStatus.fromJson(json['status'] as String),
  outOfServiceReason: json['out_of_service_reason'] as String?,
  latitude: (json['latitude'] as num?)?.toDouble(),
  longitude: (json['longitude'] as num?)?.toDouble(),
  locationUpdatedAt: json['location_updated_at'] == null
      ? null
      : DateTime.parse(json['location_updated_at'] as String),
  locationIsStale: json['location_is_stale'] as bool?,
  currentCrewCount: (json['current_crew_count'] as num?)?.toInt(),
  requiredCrewCount: (json['required_crew_count'] as num?)?.toInt(),
  isEligible: json['is_eligible'] as bool?,
  eligibilityBlockReasons: (json['eligibility_block_reasons'] as List<dynamic>?)
      ?.map((e) => AmbulanceEligibilityBlockReason.fromJson(e as String))
      .toList(),
  activeDispatchId: json['active_dispatch_id'] as String?,
  activeDispatchStatus: json['active_dispatch_status'] == null
      ? null
      : DispatchStatus.fromJson(json['active_dispatch_status'] as String),
  activeCallId: json['active_call_id'] as String?,
);

Map<String, dynamic> _$FleetMapAmbulanceToJson(FleetMapAmbulance instance) =>
    <String, dynamic>{
      'id': instance.id,
      'registration_number': instance.registrationNumber,
      'status': instance.status,
      'out_of_service_reason': instance.outOfServiceReason,
      'latitude': instance.latitude,
      'longitude': instance.longitude,
      'location_updated_at': instance.locationUpdatedAt?.toIso8601String(),
      'location_is_stale': instance.locationIsStale,
      'current_crew_count': instance.currentCrewCount,
      'required_crew_count': instance.requiredCrewCount,
      'is_eligible': instance.isEligible,
      'eligibility_block_reasons': instance.eligibilityBlockReasons,
      'active_dispatch_id': instance.activeDispatchId,
      'active_dispatch_status': instance.activeDispatchStatus,
      'active_call_id': instance.activeCallId,
    };
