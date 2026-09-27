// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'request_roster_proposal_revision_request.g.dart';

@JsonSerializable()
class RequestRosterProposalRevisionRequest {
  const RequestRosterProposalRevisionRequest({
    required this.guidance,
    this.notes,
    this.excludeStaffIds,
    this.excludeWardIds,
  });
  
  factory RequestRosterProposalRevisionRequest.fromJson(Map<String, Object?> json) => _$RequestRosterProposalRevisionRequestFromJson(json);
  
  final String guidance;
  final String? notes;
  @JsonKey(name: 'exclude_staff_ids')
  final List<String>? excludeStaffIds;
  @JsonKey(name: 'exclude_ward_ids')
  final List<String>? excludeWardIds;

  Map<String, Object?> toJson() => _$RequestRosterProposalRevisionRequestToJson(this);
}
