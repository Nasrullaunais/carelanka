// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'my_shift_dto.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

MyShiftDto _$MyShiftDtoFromJson(Map<String, dynamic> json) => MyShiftDto(
  allocationId: json['allocation_id'] as String,
  shiftId: json['shift_id'] as String,
  wardName: json['ward_name'] as String,
  date: DateTime.parse(json['date'] as String),
  startTime: json['start_time'] as String,
  endTime: json['end_time'] as String,
  crossesMidnight: json['crosses_midnight'] as bool,
  status: AllocationStatus.fromJson(json['status'] as String),
  canClockIn: json['can_clock_in'] as bool,
  wasReassigned: json['was_reassigned'] as bool,
  clockedInAt: json['clocked_in_at'] == null
      ? null
      : DateTime.parse(json['clocked_in_at'] as String),
  clockedOutAt: json['clocked_out_at'] == null
      ? null
      : DateTime.parse(json['clocked_out_at'] as String),
);

Map<String, dynamic> _$MyShiftDtoToJson(MyShiftDto instance) =>
    <String, dynamic>{
      'allocation_id': instance.allocationId,
      'shift_id': instance.shiftId,
      'ward_name': instance.wardName,
      'date': instance.date.toIso8601String(),
      'start_time': instance.startTime,
      'end_time': instance.endTime,
      'crosses_midnight': instance.crossesMidnight,
      'status': instance.status,
      'clocked_in_at': instance.clockedInAt?.toIso8601String(),
      'clocked_out_at': instance.clockedOutAt?.toIso8601String(),
      'can_clock_in': instance.canClockIn,
      'was_reassigned': instance.wasReassigned,
    };
