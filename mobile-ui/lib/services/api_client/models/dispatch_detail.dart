// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'call_priority.dart';
import 'call_status.dart';
import 'dispatch_status.dart';
import 'emergency_call_outcome.dart';

part 'dispatch_detail.g.dart';

@JsonSerializable()
class DispatchDetail {
  const DispatchDetail({
    this.id,
    this.emergencyCallId,
    this.ambulanceRegistration,
    this.callPriority,
    this.status,
    this.crewCount,
    this.acknowledgementOverdue,
    this.dispatchedAt,
    this.completedAt,
    this.ambulanceId,
    this.acknowledgedAt,
    this.acknowledgedByStaffId,
    this.declinedReason,
    this.cancellationReason,
    this.reassignmentReason,
    this.handoverNotes,
    this.patientCondition,
    this.sceneAddressLabel,
    this.destinationLabel,
    this.callStatus,
    this.callOutcome,
    this.callDetails,
    this.callerName,
    this.callerPhone,
    this.patientName,
    this.crewStaffIds,
  });
  
  factory DispatchDetail.fromJson(Map<String, Object?> json) => _$DispatchDetailFromJson(json);
  
  final String? id;
  @JsonKey(name: 'emergency_call_id')
  final String? emergencyCallId;
  @JsonKey(name: 'ambulance_registration')
  final String? ambulanceRegistration;
  @JsonKey(name: 'call_priority')
  final CallPriority? callPriority;
  final DispatchStatus? status;
  @JsonKey(name: 'crew_count')
  final int? crewCount;
  @JsonKey(name: 'acknowledgement_overdue')
  final bool? acknowledgementOverdue;
  @JsonKey(name: 'dispatched_at')
  final DateTime? dispatchedAt;
  @JsonKey(name: 'completed_at')
  final DateTime? completedAt;
  @JsonKey(name: 'ambulance_id')
  final String? ambulanceId;
  @JsonKey(name: 'acknowledged_at')
  final DateTime? acknowledgedAt;
  @JsonKey(name: 'acknowledged_by_staff_id')
  final String? acknowledgedByStaffId;
  @JsonKey(name: 'declined_reason')
  final String? declinedReason;
  @JsonKey(name: 'cancellation_reason')
  final String? cancellationReason;
  @JsonKey(name: 'reassignment_reason')
  final String? reassignmentReason;
  @JsonKey(name: 'handover_notes')
  final String? handoverNotes;
  @JsonKey(name: 'patient_condition')
  final String? patientCondition;
  @JsonKey(name: 'scene_address_label')
  final String? sceneAddressLabel;
  @JsonKey(name: 'destination_label')
  final String? destinationLabel;
  @JsonKey(name: 'call_status')
  final CallStatus? callStatus;
  @JsonKey(name: 'call_outcome')
  final EmergencyCallOutcome? callOutcome;
  @JsonKey(name: 'call_details')
  final String? callDetails;
  @JsonKey(name: 'caller_name')
  final String? callerName;
  @JsonKey(name: 'caller_phone')
  final String? callerPhone;
  @JsonKey(name: 'patient_name')
  final String? patientName;
  @JsonKey(name: 'crew_staff_ids')
  final List<String>? crewStaffIds;

  Map<String, Object?> toJson() => _$DispatchDetailToJson(this);
}
