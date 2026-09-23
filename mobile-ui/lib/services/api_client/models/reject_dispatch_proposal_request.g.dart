// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'reject_dispatch_proposal_request.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

RejectDispatchProposalRequest _$RejectDispatchProposalRequestFromJson(
  Map<String, dynamic> json,
) => RejectDispatchProposalRequest(
  reason: json['reason'] == null
      ? null
      : DispatchRejectionReason.fromJson(json['reason'] as String),
  notes: json['notes'] as String?,
);

Map<String, dynamic> _$RejectDispatchProposalRequestToJson(
  RejectDispatchProposalRequest instance,
) => <String, dynamic>{'reason': instance.reason, 'notes': instance.notes};
