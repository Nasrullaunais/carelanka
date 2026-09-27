// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'decide_leave_response.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

DecideLeaveResponse _$DecideLeaveResponseFromJson(Map<String, dynamic> json) =>
    DecideLeaveResponse(
      leaveRequest: LeaveRequestDto.fromJson(
        json['leave_request'] as Map<String, dynamic>,
      ),
      releasedAllocations: (json['released_allocations'] as List<dynamic>)
          .map((e) => AllocationSummaryDto.fromJson(e as Map<String, dynamic>))
          .toList(),
      rosterProposalIds: (json['roster_proposal_ids'] as List<dynamic>)
          .map((e) => e as String)
          .toList(),
    );

Map<String, dynamic> _$DecideLeaveResponseToJson(
  DecideLeaveResponse instance,
) => <String, dynamic>{
  'leave_request': instance.leaveRequest,
  'released_allocations': instance.releasedAllocations,
  'roster_proposal_ids': instance.rosterProposalIds,
};
