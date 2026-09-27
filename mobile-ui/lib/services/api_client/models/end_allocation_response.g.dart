// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'end_allocation_response.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

EndAllocationResponse _$EndAllocationResponseFromJson(
  Map<String, dynamic> json,
) => EndAllocationResponse(
  allocation: AllocationDto.fromJson(
    json['allocation'] as Map<String, dynamic>,
  ),
  shiftCoverage: ShiftCoverageDto.fromJson(
    json['shift_coverage'] as Map<String, dynamic>,
  ),
  rosterProposalId: json['roster_proposal_id'] as String?,
);

Map<String, dynamic> _$EndAllocationResponseToJson(
  EndAllocationResponse instance,
) => <String, dynamic>{
  'allocation': instance.allocation,
  'shift_coverage': instance.shiftCoverage,
  'roster_proposal_id': instance.rosterProposalId,
};
