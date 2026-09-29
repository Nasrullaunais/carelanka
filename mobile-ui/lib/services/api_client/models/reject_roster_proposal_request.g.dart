// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'reject_roster_proposal_request.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

RejectRosterProposalRequest _$RejectRosterProposalRequestFromJson(
  Map<String, dynamic> json,
) => RejectRosterProposalRequest(
  reason: RejectionReason.fromJson(json['reason'] as String),
  notes: json['notes'] as String?,
);

Map<String, dynamic> _$RejectRosterProposalRequestToJson(
  RejectRosterProposalRequest instance,
) => <String, dynamic>{'reason': instance.reason, 'notes': instance.notes};
