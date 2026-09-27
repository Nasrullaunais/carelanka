// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'allocation_dto.dart';
import 'shift_coverage_dto.dart';

part 'end_allocation_response.g.dart';

@JsonSerializable()
class EndAllocationResponse {
  const EndAllocationResponse({
    required this.allocation,
    required this.shiftCoverage,
    this.rosterProposalId,
  });
  
  factory EndAllocationResponse.fromJson(Map<String, Object?> json) => _$EndAllocationResponseFromJson(json);
  
  final AllocationDto allocation;
  @JsonKey(name: 'shift_coverage')
  final ShiftCoverageDto shiftCoverage;
  @JsonKey(name: 'roster_proposal_id')
  final String? rosterProposalId;

  Map<String, Object?> toJson() => _$EndAllocationResponseToJson(this);
}
