// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'allocation_dto.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

AllocationDto _$AllocationDtoFromJson(Map<String, dynamic> json) =>
    AllocationDto(
      id: json['id'] as String,
      shiftId: json['shift_id'] as String,
      staffMemberId: json['staff_member_id'] as String,
      staffName: json['staff_name'] as String,
      status: AllocationStatus.fromJson(json['status'] as String),
      source: AllocationSource.fromJson(json['source'] as String),
      createdAt: DateTime.parse(json['created_at'] as String),
      endedAt: json['ended_at'] == null
          ? null
          : DateTime.parse(json['ended_at'] as String),
      endedReason: json['ended_reason'] == null
          ? null
          : AllocationEndReason.fromJson(json['ended_reason'] as String),
      replacedByAllocationId: json['replaced_by_allocation_id'] as String?,
      clockedInAt: json['clocked_in_at'] == null
          ? null
          : DateTime.parse(json['clocked_in_at'] as String),
      clockedOutAt: json['clocked_out_at'] == null
          ? null
          : DateTime.parse(json['clocked_out_at'] as String),
      createdByStaffId: json['created_by_staff_id'] as String?,
      rosterProposalId: json['roster_proposal_id'] as String?,
    );

Map<String, dynamic> _$AllocationDtoToJson(AllocationDto instance) =>
    <String, dynamic>{
      'id': instance.id,
      'shift_id': instance.shiftId,
      'staff_member_id': instance.staffMemberId,
      'staff_name': instance.staffName,
      'status': instance.status,
      'source': instance.source,
      'ended_at': instance.endedAt?.toIso8601String(),
      'ended_reason': instance.endedReason,
      'replaced_by_allocation_id': instance.replacedByAllocationId,
      'clocked_in_at': instance.clockedInAt?.toIso8601String(),
      'clocked_out_at': instance.clockedOutAt?.toIso8601String(),
      'created_by_staff_id': instance.createdByStaffId,
      'roster_proposal_id': instance.rosterProposalId,
      'created_at': instance.createdAt.toIso8601String(),
    };
