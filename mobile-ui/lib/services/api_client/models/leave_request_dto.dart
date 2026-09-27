// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'leave_status.dart';
import 'leave_type.dart';

part 'leave_request_dto.g.dart';

@JsonSerializable()
class LeaveRequestDto {
  const LeaveRequestDto({
    required this.id,
    required this.staffMemberId,
    required this.staffName,
    required this.type,
    required this.isUrgent,
    required this.startDate,
    required this.endDate,
    required this.status,
    required this.createdAt,
    this.reason,
    this.reviewedByStaffId,
    this.reviewedAt,
    this.reviewNotes,
    this.swapWithStaffMemberId,
    this.swapShiftId,
  });
  
  factory LeaveRequestDto.fromJson(Map<String, Object?> json) => _$LeaveRequestDtoFromJson(json);
  
  final String id;
  @JsonKey(name: 'staff_member_id')
  final String staffMemberId;
  @JsonKey(name: 'staff_name')
  final String staffName;
  final LeaveType type;
  @JsonKey(name: 'is_urgent')
  final bool isUrgent;
  @JsonKey(name: 'start_date')
  final DateTime startDate;
  @JsonKey(name: 'end_date')
  final DateTime endDate;
  final String? reason;
  final LeaveStatus status;
  @JsonKey(name: 'reviewed_by_staff_id')
  final String? reviewedByStaffId;
  @JsonKey(name: 'reviewed_at')
  final DateTime? reviewedAt;
  @JsonKey(name: 'review_notes')
  final String? reviewNotes;
  @JsonKey(name: 'swap_with_staff_member_id')
  final String? swapWithStaffMemberId;
  @JsonKey(name: 'swap_shift_id')
  final String? swapShiftId;
  @JsonKey(name: 'created_at')
  final DateTime createdAt;

  Map<String, Object?> toJson() => _$LeaveRequestDtoToJson(this);
}
