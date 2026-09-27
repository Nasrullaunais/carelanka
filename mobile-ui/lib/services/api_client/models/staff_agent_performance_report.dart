// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'staff_agent_performance_report.g.dart';

@JsonSerializable()
class StaffAgentPerformanceReport {
  const StaffAgentPerformanceReport({
    this.from,
    this.to,
    this.proposalsRaised,
    this.proposalsAutoTriggered,
    this.validationFailureRate,
    this.approved,
    this.rejected,
    this.revisionRequested,
    this.failedSafely,
    this.cascadingSwaps,
    this.medianMinutesGapToFill,
    this.rejectionReasons,
  });
  
  factory StaffAgentPerformanceReport.fromJson(Map<String, Object?> json) => _$StaffAgentPerformanceReportFromJson(json);
  
  final DateTime? from;
  final DateTime? to;
  @JsonKey(name: 'proposals_raised')
  final int? proposalsRaised;
  @JsonKey(name: 'proposals_auto_triggered')
  final int? proposalsAutoTriggered;
  @JsonKey(name: 'validation_failure_rate')
  final double? validationFailureRate;
  final int? approved;
  final int? rejected;
  @JsonKey(name: 'revision_requested')
  final int? revisionRequested;
  @JsonKey(name: 'failed_safely')
  final int? failedSafely;
  @JsonKey(name: 'cascading_swaps')
  final int? cascadingSwaps;
  @JsonKey(name: 'median_minutes_gap_to_fill')
  final double? medianMinutesGapToFill;
  @JsonKey(name: 'rejection_reasons')
  final Map<String, int>? rejectionReasons;

  Map<String, Object?> toJson() => _$StaffAgentPerformanceReportToJson(this);
}
