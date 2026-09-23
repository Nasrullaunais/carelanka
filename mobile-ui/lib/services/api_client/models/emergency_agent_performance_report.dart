// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'emergency_agent_performance_report.g.dart';

@JsonSerializable()
class EmergencyAgentPerformanceReport {
  const EmergencyAgentPerformanceReport({
    this.from,
    this.to,
    this.proposalsRaised,
    this.confirmed,
    this.confirmedWithoutChangeRate,
    this.diversionsProposed,
    this.diversionsApproved,
    this.diversionsRejected,
    this.noAmbulanceAvailableCount,
    this.validationFailureRate,
    this.medianSecondsProposalToConfirm,
    this.medianMinutesCallToDispatch,
    this.rejectionReasons,
  });

  factory EmergencyAgentPerformanceReport.fromJson(Map<String, Object?> json) =>
      _$EmergencyAgentPerformanceReportFromJson(json);

  final DateTime? from;
  final DateTime? to;
  @JsonKey(name: 'proposals_raised')
  final int? proposalsRaised;
  final int? confirmed;
  @JsonKey(name: 'confirmed_without_change_rate')
  final double? confirmedWithoutChangeRate;
  @JsonKey(name: 'diversions_proposed')
  final int? diversionsProposed;
  @JsonKey(name: 'diversions_approved')
  final int? diversionsApproved;
  @JsonKey(name: 'diversions_rejected')
  final int? diversionsRejected;
  @JsonKey(name: 'no_ambulance_available_count')
  final int? noAmbulanceAvailableCount;
  @JsonKey(name: 'validation_failure_rate')
  final double? validationFailureRate;
  @JsonKey(name: 'median_seconds_proposal_to_confirm')
  final double? medianSecondsProposalToConfirm;
  @JsonKey(name: 'median_minutes_call_to_dispatch')
  final double? medianMinutesCallToDispatch;
  @JsonKey(name: 'rejection_reasons')
  final Map<String, int>? rejectionReasons;

  Map<String, Object?> toJson() =>
      _$EmergencyAgentPerformanceReportToJson(this);
}
