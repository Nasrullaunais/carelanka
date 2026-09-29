// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'allocation_summary_dto.dart';
import 'leave_request_dto.dart';

part 'decide_leave_response.g.dart';

@JsonSerializable()
class DecideLeaveResponse {
  const DecideLeaveResponse({
    required this.leaveRequest,
    required this.releasedAllocations,
    required this.rosterProposalIds,
  });
  
  factory DecideLeaveResponse.fromJson(Map<String, Object?> json) => _$DecideLeaveResponseFromJson(json);
  
  @JsonKey(name: 'leave_request')
  final LeaveRequestDto leaveRequest;
  @JsonKey(name: 'released_allocations')
  final List<AllocationSummaryDto> releasedAllocations;
  @JsonKey(name: 'roster_proposal_ids')
  final List<String> rosterProposalIds;

  Map<String, Object?> toJson() => _$DecideLeaveResponseToJson(this);
}
