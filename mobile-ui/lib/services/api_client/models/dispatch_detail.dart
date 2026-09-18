// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'call_priority.dart';
import 'dispatch_status.dart';

part 'dispatch_detail.g.dart';

@JsonSerializable()
class DispatchDetail {
  const DispatchDetail({
    this.id,
    this.emergencyCallId,
    this.ambulanceRegistration,
    this.callPriority,
    this.status,
    this.destinationWardName,
    this.crewCount,
    this.dispatchedAt,
    this.completedAt,
    this.ambulanceId,
    this.acknowledgedAt,
    this.acknowledgedByStaffId,
    this.declinedReason,
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
  @JsonKey(name: 'destination_ward_name')
  final String? destinationWardName;
  @JsonKey(name: 'crew_count')
  final int? crewCount;
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
  @JsonKey(name: 'crew_staff_ids')
  final List<String>? crewStaffIds;

  Map<String, Object?> toJson() => _$DispatchDetailToJson(this);
}
