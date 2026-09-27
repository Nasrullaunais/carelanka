// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'create_leave_request.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

CreateLeaveRequest _$CreateLeaveRequestFromJson(Map<String, dynamic> json) =>
    CreateLeaveRequest(
      type: LeaveType.fromJson(json['type'] as String),
      startDate: json['start_date'] == null
          ? null
          : DateTime.parse(json['start_date'] as String),
      endDate: json['end_date'] == null
          ? null
          : DateTime.parse(json['end_date'] as String),
      reason: json['reason'] as String?,
      swapShiftId: json['swap_shift_id'] as String?,
      swapWithStaffMemberId: json['swap_with_staff_member_id'] as String?,
    );

Map<String, dynamic> _$CreateLeaveRequestToJson(CreateLeaveRequest instance) =>
    <String, dynamic>{
      'type': instance.type,
      'start_date': instance.startDate?.toIso8601String(),
      'end_date': instance.endDate?.toIso8601String(),
      'reason': instance.reason,
      'swap_shift_id': instance.swapShiftId,
      'swap_with_staff_member_id': instance.swapWithStaffMemberId,
    };
