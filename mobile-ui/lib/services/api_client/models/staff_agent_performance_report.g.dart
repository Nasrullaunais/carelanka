// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'staff_agent_performance_report.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

StaffAgentPerformanceReport _$StaffAgentPerformanceReportFromJson(
  Map<String, dynamic> json,
) => StaffAgentPerformanceReport(
  from: json['from'] == null ? null : DateTime.parse(json['from'] as String),
  to: json['to'] == null ? null : DateTime.parse(json['to'] as String),
  proposalsRaised: (json['proposals_raised'] as num?)?.toInt(),
  proposalsAutoTriggered: (json['proposals_auto_triggered'] as num?)?.toInt(),
  validationFailureRate: (json['validation_failure_rate'] as num?)?.toDouble(),
  approved: (json['approved'] as num?)?.toInt(),
  rejected: (json['rejected'] as num?)?.toInt(),
  revisionRequested: (json['revision_requested'] as num?)?.toInt(),
  failedSafely: (json['failed_safely'] as num?)?.toInt(),
  cascadingSwaps: (json['cascading_swaps'] as num?)?.toInt(),
  medianMinutesGapToFill: (json['median_minutes_gap_to_fill'] as num?)
      ?.toDouble(),
  rejectionReasons: (json['rejection_reasons'] as Map<String, dynamic>?)?.map(
    (k, e) => MapEntry(k, (e as num).toInt()),
  ),
);

Map<String, dynamic> _$StaffAgentPerformanceReportToJson(
  StaffAgentPerformanceReport instance,
) => <String, dynamic>{
  'from': instance.from?.toIso8601String(),
  'to': instance.to?.toIso8601String(),
  'proposals_raised': instance.proposalsRaised,
  'proposals_auto_triggered': instance.proposalsAutoTriggered,
  'validation_failure_rate': instance.validationFailureRate,
  'approved': instance.approved,
  'rejected': instance.rejected,
  'revision_requested': instance.revisionRequested,
  'failed_safely': instance.failedSafely,
  'cascading_swaps': instance.cascadingSwaps,
  'median_minutes_gap_to_fill': instance.medianMinutesGapToFill,
  'rejection_reasons': instance.rejectionReasons,
};
