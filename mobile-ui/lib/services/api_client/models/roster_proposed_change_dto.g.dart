// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'roster_proposed_change_dto.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

RosterProposedChangeDto _$RosterProposedChangeDtoFromJson(
  Map<String, dynamic> json,
) => RosterProposedChangeDto(
  id: json['id'] as String?,
  sequence: (json['sequence'] as num?)?.toInt(),
  changeType: json['change_type'] == null
      ? null
      : RosterProposedChangeType.fromJson(json['change_type'] as String),
  targetAllocationId: json['target_allocation_id'] as String?,
  proposedStaffMemberId: json['proposed_staff_member_id'] as String?,
  staffMemberId: json['staff_member_id'] as String?,
  proposedStaffName: json['proposed_staff_name'] as String?,
  staffName: json['staff_name'] as String?,
  proposedShiftId: json['proposed_shift_id'] as String?,
  toShiftId: json['to_shift_id'] as String?,
  fromShiftId: json['from_shift_id'] as String?,
  fromWardName: json['from_ward_name'] as String?,
  toWardName: json['to_ward_name'] as String?,
  rationale: json['rationale'] as String?,
  validationStatus: json['validation_status'] == null
      ? null
      : ProposedChangeValidationStatus.fromJson(
          json['validation_status'] as String,
        ),
  validationMessage: json['validation_message'] as String?,
  appliedAt: json['applied_at'] == null
      ? null
      : DateTime.parse(json['applied_at'] as String),
  appliedEntityId: json['applied_entity_id'] as String?,
);

Map<String, dynamic> _$RosterProposedChangeDtoToJson(
  RosterProposedChangeDto instance,
) => <String, dynamic>{
  'id': instance.id,
  'sequence': instance.sequence,
  'change_type': instance.changeType,
  'target_allocation_id': instance.targetAllocationId,
  'proposed_staff_member_id': instance.proposedStaffMemberId,
  'staff_member_id': instance.staffMemberId,
  'proposed_staff_name': instance.proposedStaffName,
  'staff_name': instance.staffName,
  'proposed_shift_id': instance.proposedShiftId,
  'to_shift_id': instance.toShiftId,
  'from_shift_id': instance.fromShiftId,
  'from_ward_name': instance.fromWardName,
  'to_ward_name': instance.toWardName,
  'rationale': instance.rationale,
  'validation_status': instance.validationStatus,
  'validation_message': instance.validationMessage,
  'applied_at': instance.appliedAt?.toIso8601String(),
  'applied_entity_id': instance.appliedEntityId,
};
