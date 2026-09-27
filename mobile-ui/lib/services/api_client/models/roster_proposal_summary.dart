// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'agent_outcome.dart';
import 'roster_proposal_status.dart';

part 'roster_proposal_summary.g.dart';

@JsonSerializable()
class RosterProposalSummary {
  const RosterProposalSummary({
    this.id,
    this.workflowId,
    this.shiftId,
    this.wardName,
    this.shiftDate,
    this.objective,
    this.status,
    this.outcome,
    this.isCascadingSwap,
    this.changeCount,
    this.createdAt,
  });
  
  factory RosterProposalSummary.fromJson(Map<String, Object?> json) => _$RosterProposalSummaryFromJson(json);
  
  final String? id;
  @JsonKey(name: 'workflow_id')
  final String? workflowId;
  @JsonKey(name: 'shift_id')
  final String? shiftId;
  @JsonKey(name: 'ward_name')
  final String? wardName;
  @JsonKey(name: 'shift_date')
  final DateTime? shiftDate;
  final String? objective;
  final RosterProposalStatus? status;
  final AgentOutcome? outcome;
  @JsonKey(name: 'is_cascading_swap')
  final bool? isCascadingSwap;
  @JsonKey(name: 'change_count')
  final int? changeCount;
  @JsonKey(name: 'created_at')
  final DateTime? createdAt;

  Map<String, Object?> toJson() => _$RosterProposalSummaryToJson(this);
}
