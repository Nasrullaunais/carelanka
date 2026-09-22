// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'call_priority.dart';
import 'call_status.dart';

part 'emergency_call_summary.g.dart';

@JsonSerializable()
class EmergencyCallSummary {
  const EmergencyCallSummary({
    this.id,
    this.priority,
    this.status,
    this.callerName,
    this.addressLabel,
    this.latitude,
    this.longitude,
    this.activeDispatchId,
    this.openProposalId,
    this.waitingMinutes,
    this.createdAt,
  });
  
  factory EmergencyCallSummary.fromJson(Map<String, Object?> json) => _$EmergencyCallSummaryFromJson(json);
  
  final String? id;
  final CallPriority? priority;
  final CallStatus? status;
  @JsonKey(name: 'caller_name')
  final String? callerName;
  @JsonKey(name: 'address_label')
  final String? addressLabel;
  final double? latitude;
  final double? longitude;
  @JsonKey(name: 'active_dispatch_id')
  final String? activeDispatchId;
  @JsonKey(name: 'open_proposal_id')
  final String? openProposalId;
  @JsonKey(name: 'waiting_minutes')
  final int? waitingMinutes;
  @JsonKey(name: 'created_at')
  final DateTime? createdAt;

  Map<String, Object?> toJson() => _$EmergencyCallSummaryToJson(this);
}
