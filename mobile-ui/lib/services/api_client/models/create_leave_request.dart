// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'leave_type.dart';

part 'create_leave_request.g.dart';

@JsonSerializable()
class CreateLeaveRequest {
  const CreateLeaveRequest({
    required this.type,
    this.startDate,
    this.endDate,
    this.reason,
    this.swapShiftId,
    this.swapWithStaffMemberId,
  });
  
  factory CreateLeaveRequest.fromJson(Map<String, Object?> json) => _$CreateLeaveRequestFromJson(json);
  
  final LeaveType type;
  @JsonKey(name: 'start_date')
  final DateTime? startDate;
  @JsonKey(name: 'end_date')
  final DateTime? endDate;
  final String? reason;
  @JsonKey(name: 'swap_shift_id')
  final String? swapShiftId;
  @JsonKey(name: 'swap_with_staff_member_id')
  final String? swapWithStaffMemberId;

  Map<String, Object?> toJson() => _$CreateLeaveRequestToJson(this);
}
