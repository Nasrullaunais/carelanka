// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'dispatch_rejection_reason.dart';

part 'reject_dispatch_proposal_request.g.dart';

@JsonSerializable()
class RejectDispatchProposalRequest {
  const RejectDispatchProposalRequest({this.reason, this.notes});

  factory RejectDispatchProposalRequest.fromJson(Map<String, Object?> json) =>
      _$RejectDispatchProposalRequestFromJson(json);

  final DispatchRejectionReason? reason;
  final String? notes;

  Map<String, Object?> toJson() => _$RejectDispatchProposalRequestToJson(this);
}
