// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'proposed_change_validation_status.dart';
import 'roster_proposed_change_type.dart';

part 'roster_proposed_change_dto.g.dart';

@JsonSerializable()
class RosterProposedChangeDto {
  const RosterProposedChangeDto({
    this.id,
    this.sequence,
    this.changeType,
    this.targetAllocationId,
    this.proposedStaffMemberId,
    this.staffMemberId,
    this.proposedStaffName,
    this.staffName,
    this.proposedShiftId,
    this.toShiftId,
    this.fromShiftId,
    this.fromWardName,
    this.toWardName,
    this.rationale,
    this.validationStatus,
    this.validationMessage,
    this.appliedAt,
    this.appliedEntityId,
  });
  
  factory RosterProposedChangeDto.fromJson(Map<String, Object?> json) => _$RosterProposedChangeDtoFromJson(json);
  
  final String? id;
  final int? sequence;
  @JsonKey(name: 'change_type')
  final RosterProposedChangeType? changeType;
  @JsonKey(name: 'target_allocation_id')
  final String? targetAllocationId;
  @JsonKey(name: 'proposed_staff_member_id')
  final String? proposedStaffMemberId;
  @JsonKey(name: 'staff_member_id')
  final String? staffMemberId;
  @JsonKey(name: 'proposed_staff_name')
  final String? proposedStaffName;
  @JsonKey(name: 'staff_name')
  final String? staffName;
  @JsonKey(name: 'proposed_shift_id')
  final String? proposedShiftId;
  @JsonKey(name: 'to_shift_id')
  final String? toShiftId;
  @JsonKey(name: 'from_shift_id')
  final String? fromShiftId;
  @JsonKey(name: 'from_ward_name')
  final String? fromWardName;
  @JsonKey(name: 'to_ward_name')
  final String? toWardName;
  final String? rationale;
  @JsonKey(name: 'validation_status')
  final ProposedChangeValidationStatus? validationStatus;
  @JsonKey(name: 'validation_message')
  final String? validationMessage;
  @JsonKey(name: 'applied_at')
  final DateTime? appliedAt;
  @JsonKey(name: 'applied_entity_id')
  final String? appliedEntityId;

  Map<String, Object?> toJson() => _$RosterProposedChangeDtoToJson(this);
}
