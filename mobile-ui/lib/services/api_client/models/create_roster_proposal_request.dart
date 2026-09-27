// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'create_roster_proposal_request.g.dart';

@JsonSerializable()
class CreateRosterProposalRequest {
  const CreateRosterProposalRequest({
    required this.shiftId,
    this.objective,
    this.allowCascadingSwap,
  });
  
  factory CreateRosterProposalRequest.fromJson(Map<String, Object?> json) => _$CreateRosterProposalRequestFromJson(json);
  
  @JsonKey(name: 'shift_id')
  final String shiftId;
  final String? objective;
  @JsonKey(name: 'allow_cascading_swap')
  final bool? allowCascadingSwap;

  Map<String, Object?> toJson() => _$CreateRosterProposalRequestToJson(this);
}
