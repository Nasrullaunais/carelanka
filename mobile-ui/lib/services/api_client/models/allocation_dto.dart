// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'allocation_end_reason.dart';
import 'allocation_source.dart';
import 'allocation_status.dart';

part 'allocation_dto.g.dart';

@JsonSerializable()
class AllocationDto {
  const AllocationDto({
    required this.id,
    required this.shiftId,
    required this.staffMemberId,
    required this.staffName,
    required this.status,
    required this.source,
    required this.createdAt,
    this.endedAt,
    this.endedReason,
    this.replacedByAllocationId,
    this.clockedInAt,
    this.clockedOutAt,
    this.createdByStaffId,
    this.rosterProposalId,
  });
  
  factory AllocationDto.fromJson(Map<String, Object?> json) => _$AllocationDtoFromJson(json);
  
  final String id;
  @JsonKey(name: 'shift_id')
  final String shiftId;
  @JsonKey(name: 'staff_member_id')
  final String staffMemberId;
  @JsonKey(name: 'staff_name')
  final String staffName;
  final AllocationStatus status;
  final AllocationSource source;
  @JsonKey(name: 'ended_at')
  final DateTime? endedAt;
  @JsonKey(name: 'ended_reason')
  final AllocationEndReason? endedReason;
  @JsonKey(name: 'replaced_by_allocation_id')
  final String? replacedByAllocationId;
  @JsonKey(name: 'clocked_in_at')
  final DateTime? clockedInAt;
  @JsonKey(name: 'clocked_out_at')
  final DateTime? clockedOutAt;
  @JsonKey(name: 'created_by_staff_id')
  final String? createdByStaffId;
  @JsonKey(name: 'roster_proposal_id')
  final String? rosterProposalId;
  @JsonKey(name: 'created_at')
  final DateTime createdAt;

  Map<String, Object?> toJson() => _$AllocationDtoToJson(this);
}
