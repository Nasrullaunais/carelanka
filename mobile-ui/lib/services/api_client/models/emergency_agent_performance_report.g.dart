// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'emergency_agent_performance_report.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

EmergencyAgentPerformanceReport _$EmergencyAgentPerformanceReportFromJson(
  Map<String, dynamic> json,
) => EmergencyAgentPerformanceReport(
  from: json['from'] == null ? null : DateTime.parse(json['from'] as String),
  to: json['to'] == null ? null : DateTime.parse(json['to'] as String),
  proposalsRaised: (json['proposals_raised'] as num?)?.toInt(),
  confirmed: (json['confirmed'] as num?)?.toInt(),
  confirmedWithoutChangeRate: (json['confirmed_without_change_rate'] as num?)
      ?.toDouble(),
  diversionsProposed: (json['diversions_proposed'] as num?)?.toInt(),
  diversionsApproved: (json['diversions_approved'] as num?)?.toInt(),
  diversionsRejected: (json['diversions_rejected'] as num?)?.toInt(),
  noAmbulanceAvailableCount: (json['no_ambulance_available_count'] as num?)
      ?.toInt(),
  validationFailureRate: (json['validation_failure_rate'] as num?)?.toDouble(),
  medianSecondsProposalToConfirm:
      (json['median_seconds_proposal_to_confirm'] as num?)?.toDouble(),
  medianMinutesCallToDispatch: (json['median_minutes_call_to_dispatch'] as num?)
      ?.toDouble(),
  rejectionReasons: (json['rejection_reasons'] as Map<String, dynamic>?)?.map(
    (k, e) => MapEntry(k, (e as num).toInt()),
  ),
);

Map<String, dynamic> _$EmergencyAgentPerformanceReportToJson(
  EmergencyAgentPerformanceReport instance,
) => <String, dynamic>{
  'from': instance.from?.toIso8601String(),
  'to': instance.to?.toIso8601String(),
  'proposals_raised': instance.proposalsRaised,
  'confirmed': instance.confirmed,
  'confirmed_without_change_rate': instance.confirmedWithoutChangeRate,
  'diversions_proposed': instance.diversionsProposed,
  'diversions_approved': instance.diversionsApproved,
  'diversions_rejected': instance.diversionsRejected,
  'no_ambulance_available_count': instance.noAmbulanceAvailableCount,
  'validation_failure_rate': instance.validationFailureRate,
  'median_seconds_proposal_to_confirm': instance.medianSecondsProposalToConfirm,
  'median_minutes_call_to_dispatch': instance.medianMinutesCallToDispatch,
  'rejection_reasons': instance.rejectionReasons,
};
