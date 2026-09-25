// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'call_priority.dart';
import 'dispatch_outcome.dart';
import 'dispatch_proposal_status.dart';

part 'dispatch_proposal_summary.g.dart';

@JsonSerializable()
class DispatchProposalSummary {
  const DispatchProposalSummary({
    this.id,
    this.workflowId,
    this.emergencyCallId,
    this.callPriority,
    this.status,
    this.outcome,
    this.isDiversion,
    this.proposedAmbulanceRegistration,
    this.estimatedMinutesToScene,
    this.createdAt,
  });

  factory DispatchProposalSummary.fromJson(Map<String, Object?> json) =>
      _$DispatchProposalSummaryFromJson(json);

  final String? id;
  @JsonKey(name: 'workflow_id')
  final String? workflowId;
  @JsonKey(name: 'emergency_call_id')
  final String? emergencyCallId;
  @JsonKey(name: 'call_priority')
  final CallPriority? callPriority;
  final DispatchProposalStatus? status;
  final DispatchOutcome? outcome;
  @JsonKey(name: 'is_diversion')
  final bool? isDiversion;
  @JsonKey(name: 'proposed_ambulance_registration')
  final String? proposedAmbulanceRegistration;
  @JsonKey(name: 'estimated_minutes_to_scene')
  final int? estimatedMinutesToScene;
  @JsonKey(name: 'created_at')
  final DateTime? createdAt;

  Map<String, Object?> toJson() => _$DispatchProposalSummaryToJson(this);
}
