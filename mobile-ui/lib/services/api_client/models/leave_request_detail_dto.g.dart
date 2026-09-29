// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'leave_request_detail_dto.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

LeaveRequestDetailDto _$LeaveRequestDetailDtoFromJson(
  Map<String, dynamic> json,
) => LeaveRequestDetailDto(
  id: json['id'] as String,
  staffMemberId: json['staff_member_id'] as String,
  staffName: json['staff_name'] as String,
  type: LeaveType.fromJson(json['type'] as String),
  isUrgent: json['is_urgent'] as bool,
  startDate: DateTime.parse(json['start_date'] as String),
  endDate: DateTime.parse(json['end_date'] as String),
  status: LeaveStatus.fromJson(json['status'] as String),
  createdAt: DateTime.parse(json['created_at'] as String),
  affectedShifts: (json['affected_shifts'] as List<dynamic>)
      .map((e) => AffectedShiftDto.fromJson(e as Map<String, dynamic>))
      .toList(),
  reason: json['reason'] as String?,
  reviewedByStaffId: json['reviewed_by_staff_id'] as String?,
  reviewedAt: json['reviewed_at'] == null
      ? null
      : DateTime.parse(json['reviewed_at'] as String),
  reviewNotes: json['review_notes'] as String?,
  swapWithStaffMemberId: json['swap_with_staff_member_id'] as String?,
  swapShiftId: json['swap_shift_id'] as String?,
);

Map<String, dynamic> _$LeaveRequestDetailDtoToJson(
  LeaveRequestDetailDto instance,
) => <String, dynamic>{
  'id': instance.id,
  'staff_member_id': instance.staffMemberId,
  'staff_name': instance.staffName,
  'type': instance.type,
  'is_urgent': instance.isUrgent,
  'start_date': instance.startDate.toIso8601String(),
  'end_date': instance.endDate.toIso8601String(),
  'reason': instance.reason,
  'status': instance.status,
  'reviewed_by_staff_id': instance.reviewedByStaffId,
  'reviewed_at': instance.reviewedAt?.toIso8601String(),
  'review_notes': instance.reviewNotes,
  'swap_with_staff_member_id': instance.swapWithStaffMemberId,
  'swap_shift_id': instance.swapShiftId,
  'created_at': instance.createdAt.toIso8601String(),
  'affected_shifts': instance.affectedShifts,
};
