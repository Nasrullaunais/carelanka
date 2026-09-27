// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'emergency_cancellation_request.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

EmergencyCancellationRequest _$EmergencyCancellationRequestFromJson(
  Map<String, dynamic> json,
) => EmergencyCancellationRequest(
  emergencyCallId: json['emergency_call_id'] as String?,
  callPriority: json['call_priority'] == null
      ? null
      : CallPriority.fromJson(json['call_priority'] as String),
  callStatus: json['call_status'] == null
      ? null
      : CallStatus.fromJson(json['call_status'] as String),
  callerName: json['caller_name'] as String?,
  addressLabel: json['address_label'] as String?,
  callCreatedAt: json['call_created_at'] == null
      ? null
      : DateTime.parse(json['call_created_at'] as String),
  activeAmbulanceRegistration: json['active_ambulance_registration'] as String?,
  status: json['status'] == null
      ? null
      : CancellationRequestStatus.fromJson(json['status'] as String),
  reason: json['reason'] as String?,
  requestedAt: json['requested_at'] == null
      ? null
      : DateTime.parse(json['requested_at'] as String),
  reviewedAt: json['reviewed_at'] == null
      ? null
      : DateTime.parse(json['reviewed_at'] as String),
  reviewedByStaffId: json['reviewed_by_staff_id'] as String?,
  reviewNotes: json['review_notes'] as String?,
);

Map<String, dynamic> _$EmergencyCancellationRequestToJson(
  EmergencyCancellationRequest instance,
) => <String, dynamic>{
  'emergency_call_id': instance.emergencyCallId,
  'call_priority': instance.callPriority,
  'call_status': instance.callStatus,
  'caller_name': instance.callerName,
  'address_label': instance.addressLabel,
  'call_created_at': instance.callCreatedAt?.toIso8601String(),
  'active_ambulance_registration': instance.activeAmbulanceRegistration,
  'status': instance.status,
  'reason': instance.reason,
  'requested_at': instance.requestedAt?.toIso8601String(),
  'reviewed_at': instance.reviewedAt?.toIso8601String(),
  'reviewed_by_staff_id': instance.reviewedByStaffId,
  'review_notes': instance.reviewNotes,
};
