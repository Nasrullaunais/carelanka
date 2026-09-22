// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'dispatch_summary.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

DispatchSummary _$DispatchSummaryFromJson(Map<String, dynamic> json) =>
    DispatchSummary(
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
      acknowledgementOverdue: json['acknowledgement_overdue'] as bool?,
      dispatchedAt: json['dispatched_at'] == null
          ? null
          : DateTime.parse(json['dispatched_at'] as String),
      completedAt: json['completed_at'] == null
          ? null
          : DateTime.parse(json['completed_at'] as String),
    );

Map<String, dynamic> _$DispatchSummaryToJson(DispatchSummary instance) =>
    <String, dynamic>{
      'id': instance.id,
      'emergency_call_id': instance.emergencyCallId,
      'ambulance_registration': instance.ambulanceRegistration,
      'call_priority': instance.callPriority,
      'status': instance.status,
      'destination_ward_name': instance.destinationWardName,
      'crew_count': instance.crewCount,
      'acknowledgement_overdue': instance.acknowledgementOverdue,
      'dispatched_at': instance.dispatchedAt?.toIso8601String(),
      'completed_at': instance.completedAt?.toIso8601String(),
    };
