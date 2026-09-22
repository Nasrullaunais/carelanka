// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'call_priority.dart';
import 'dispatch_status.dart';

part 'dispatch_summary.g.dart';

@JsonSerializable()
class DispatchSummary {
  const DispatchSummary({
    this.id,
    this.emergencyCallId,
    this.ambulanceRegistration,
    this.callPriority,
    this.status,
    this.destinationWardName,
    this.crewCount,
    this.acknowledgementOverdue,
    this.dispatchedAt,
    this.completedAt,
  });
  
  factory DispatchSummary.fromJson(Map<String, Object?> json) => _$DispatchSummaryFromJson(json);
  
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
  @JsonKey(name: 'acknowledgement_overdue')
  final bool? acknowledgementOverdue;
  @JsonKey(name: 'dispatched_at')
  final DateTime? dispatchedAt;
  @JsonKey(name: 'completed_at')
  final DateTime? completedAt;

  Map<String, Object?> toJson() => _$DispatchSummaryToJson(this);
}
