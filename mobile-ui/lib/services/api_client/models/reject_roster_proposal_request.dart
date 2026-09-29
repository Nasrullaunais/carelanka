// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'rejection_reason.dart';

part 'reject_roster_proposal_request.g.dart';

@JsonSerializable()
class RejectRosterProposalRequest {
  const RejectRosterProposalRequest({
    required this.reason,
    this.notes,
  });
  
  factory RejectRosterProposalRequest.fromJson(Map<String, Object?> json) => _$RejectRosterProposalRequestFromJson(json);
  
  final RejectionReason reason;
  final String? notes;

  Map<String, Object?> toJson() => _$RejectRosterProposalRequestToJson(this);
}
