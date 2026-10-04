// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'fleet_map_call.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

FleetMapCall _$FleetMapCallFromJson(Map<String, dynamic> json) => FleetMapCall(
  id: json['id'] as String?,
  priority: json['priority'] == null
      ? null
      : CallPriority.fromJson(json['priority'] as String),
  status: json['status'] == null
      ? null
      : CallStatus.fromJson(json['status'] as String),
  addressLabel: json['address_label'] as String?,
  latitude: (json['latitude'] as num?)?.toDouble(),
  longitude: (json['longitude'] as num?)?.toDouble(),
  waitingMinutes: (json['waiting_minutes'] as num?)?.toInt(),
  assignedAmbulanceId: json['assigned_ambulance_id'] as String?,
  createdAt: json['created_at'] == null
      ? null
      : DateTime.parse(json['created_at'] as String),
);

Map<String, dynamic> _$FleetMapCallToJson(FleetMapCall instance) =>
    <String, dynamic>{
      'id': instance.id,
      'priority': instance.priority,
      'status': instance.status,
      'address_label': instance.addressLabel,
      'latitude': instance.latitude,
      'longitude': instance.longitude,
      'waiting_minutes': instance.waitingMinutes,
      'assigned_ambulance_id': instance.assignedAmbulanceId,
      'created_at': instance.createdAt?.toIso8601String(),
    };
