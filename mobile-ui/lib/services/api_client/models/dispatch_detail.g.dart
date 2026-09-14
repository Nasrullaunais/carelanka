// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'dispatch_detail.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

DispatchDetail _$DispatchDetailFromJson(Map<String, dynamic> json) =>
    DispatchDetail(
      id: json['id'] as String?,
      emergencyCallId: json['emergency_call_id'] as String?,
      ambulanceRegistration: json['ambulance_registration'] as String?,
      callPriority: json['call_priority'] == null
          ? null
          : CallPriority.fromJson(json['call_priority'] as String),
      status: json['status'] == null
          ? null
          : DispatchStatus.fromJson(json['status'] as String),
      destinationWardName: json['destination_ward_name'] as String?,
      crewCount: (json['crew_count'] as num?)?.toInt(),
      dispatchedAt: json['dispatched_at'] == null
          ? null
          : DateTime.parse(json['dispatched_at'] as String),
      completedAt: json['completed_at'] == null
          ? null
          : DateTime.parse(json['completed_at'] as String),
      ambulanceId: json['ambulance_id'] as String?,
      acknowledgedAt: json['acknowledged_at'] == null
          ? null
          : DateTime.parse(json['acknowledged_at'] as String),
      acknowledgedByStaffId: json['acknowledged_by_staff_id'] as String?,
      declinedReason: json['declined_reason'] as String?,
      crewStaffIds: (json['crew_staff_ids'] as List<dynamic>?)
          ?.map((e) => e as String)
          .toList(),
    );

Map<String, dynamic> _$DispatchDetailToJson(DispatchDetail instance) =>
    <String, dynamic>{
      'id': instance.id,
      'emergency_call_id': instance.emergencyCallId,
      'ambulance_registration': instance.ambulanceRegistration,
      'call_priority': instance.callPriority,
      'status': instance.status,
      'destination_ward_name': instance.destinationWardName,
      'crew_count': instance.crewCount,
      'dispatched_at': instance.dispatchedAt?.toIso8601String(),
      'completed_at': instance.completedAt?.toIso8601String(),
      'ambulance_id': instance.ambulanceId,
      'acknowledged_at': instance.acknowledgedAt?.toIso8601String(),
      'acknowledged_by_staff_id': instance.acknowledgedByStaffId,
      'declined_reason': instance.declinedReason,
      'crew_staff_ids': instance.crewStaffIds,
    };
