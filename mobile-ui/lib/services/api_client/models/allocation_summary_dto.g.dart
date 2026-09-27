// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'allocation_summary_dto.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

AllocationSummaryDto _$AllocationSummaryDtoFromJson(
  Map<String, dynamic> json,
) => AllocationSummaryDto(
  allocationId: json['allocation_id'] as String,
  shiftId: json['shift_id'] as String,
  wardName: json['ward_name'] as String,
  date: DateTime.parse(json['date'] as String),
  startTime: json['start_time'] as String,
  endTime: json['end_time'] as String,
  staffMemberId: json['staff_member_id'] as String,
  staffName: json['staff_name'] as String,
  status: AllocationStatus.fromJson(json['status'] as String),
);

Map<String, dynamic> _$AllocationSummaryDtoToJson(
  AllocationSummaryDto instance,
) => <String, dynamic>{
  'allocation_id': instance.allocationId,
  'shift_id': instance.shiftId,
  'ward_name': instance.wardName,
  'date': instance.date.toIso8601String(),
  'start_time': instance.startTime,
  'end_time': instance.endTime,
  'staff_member_id': instance.staffMemberId,
  'staff_name': instance.staffName,
  'status': instance.status,
};
