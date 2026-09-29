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
      acknowledgementOverdue: json['acknowledgement_overdue'] as bool?,
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
      cancellationReason: json['cancellation_reason'] as String?,
      reassignmentReason: json['reassignment_reason'] as String?,
      handoverNotes: json['handover_notes'] as String?,
      patientCondition: json['patient_condition'] as String?,
      sceneAddressLabel: json['scene_address_label'] as String?,
      sceneDetails: json['scene_details'] as String?,
      sceneLatitude: (json['scene_latitude'] as num?)?.toDouble(),
      sceneLongitude: (json['scene_longitude'] as num?)?.toDouble(),
      sceneLocationAccuracyMetres:
          (json['scene_location_accuracy_metres'] as num?)?.toDouble(),
      callerName: json['caller_name'] as String?,
      callerPhone: json['caller_phone'] as String?,
      patientIsCaller: json['patient_is_caller'] as bool?,
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
      'acknowledgement_overdue': instance.acknowledgementOverdue,
      'dispatched_at': instance.dispatchedAt?.toIso8601String(),
      'completed_at': instance.completedAt?.toIso8601String(),
      'ambulance_id': instance.ambulanceId,
      'acknowledged_at': instance.acknowledgedAt?.toIso8601String(),
      'acknowledged_by_staff_id': instance.acknowledgedByStaffId,
      'declined_reason': instance.declinedReason,
      'cancellation_reason': instance.cancellationReason,
      'reassignment_reason': instance.reassignmentReason,
      'handover_notes': instance.handoverNotes,
      'patient_condition': instance.patientCondition,
      'scene_address_label': instance.sceneAddressLabel,
      'scene_details': instance.sceneDetails,
      'scene_latitude': instance.sceneLatitude,
      'scene_longitude': instance.sceneLongitude,
      'scene_location_accuracy_metres': instance.sceneLocationAccuracyMetres,
      'caller_name': instance.callerName,
      'caller_phone': instance.callerPhone,
      'patient_is_caller': instance.patientIsCaller,
      'crew_staff_ids': instance.crewStaffIds,
    };
