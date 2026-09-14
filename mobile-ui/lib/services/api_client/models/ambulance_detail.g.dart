// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'ambulance_detail.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

AmbulanceDetail _$AmbulanceDetailFromJson(Map<String, dynamic> json) =>
    AmbulanceDetail(
      id: json['id'] as String,
      registrationNumber: json['registration_number'] as String,
      status: AmbulanceStatus.fromJson(json['status'] as String),
      isActive: json['is_active'] as bool,
      createdAt: DateTime.parse(json['created_at'] as String),
      updatedAt: DateTime.parse(json['updated_at'] as String),
      currentLatitude: (json['current_latitude'] as num?)?.toDouble(),
      currentLongitude: (json['current_longitude'] as num?)?.toDouble(),
      locationUpdatedAt: json['location_updated_at'] == null
          ? null
          : DateTime.parse(json['location_updated_at'] as String),
      outOfServiceReason: json['out_of_service_reason'] as String?,
      activeDispatch: json['active_dispatch'] == null
          ? null
          : DispatchSummary.fromJson(
              json['active_dispatch'] as Map<String, dynamic>,
            ),
      isDivertible: json['is_divertible'] as bool?,
      runsToday: (json['runs_today'] as num?)?.toInt(),
      currentCrew: (json['current_crew'] as List<dynamic>?)
          ?.map(
            (e) => AmbulanceCrewAssignment.fromJson(e as Map<String, dynamic>),
          )
          .toList(),
    );

Map<String, dynamic> _$AmbulanceDetailToJson(AmbulanceDetail instance) =>
    <String, dynamic>{
      'id': instance.id,
      'registration_number': instance.registrationNumber,
      'current_latitude': instance.currentLatitude,
      'current_longitude': instance.currentLongitude,
      'location_updated_at': instance.locationUpdatedAt?.toIso8601String(),
      'status': instance.status,
      'out_of_service_reason': instance.outOfServiceReason,
      'is_active': instance.isActive,
      'created_at': instance.createdAt.toIso8601String(),
      'updated_at': instance.updatedAt.toIso8601String(),
      'active_dispatch': instance.activeDispatch,
      'is_divertible': instance.isDivertible,
      'runs_today': instance.runsToday,
      'current_crew': instance.currentCrew,
    };
