// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'approve_roster_proposal_request.g.dart';

@JsonSerializable()
class ApproveRosterProposalRequest {
  const ApproveRosterProposalRequest({
    this.notes,
  });
  
  factory ApproveRosterProposalRequest.fromJson(Map<String, Object?> json) => _$ApproveRosterProposalRequestFromJson(json);
  
  final String? notes;

  Map<String, Object?> toJson() => _$ApproveRosterProposalRequestToJson(this);
}
