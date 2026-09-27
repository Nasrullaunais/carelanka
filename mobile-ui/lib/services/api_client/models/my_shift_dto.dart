// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'allocation_status.dart';

part 'my_shift_dto.g.dart';

@JsonSerializable()
class MyShiftDto {
  const MyShiftDto({
    required this.allocationId,
    required this.shiftId,
    required this.wardName,
    required this.date,
    required this.startTime,
    required this.endTime,
    required this.crossesMidnight,
    required this.status,
    required this.canClockIn,
    required this.wasReassigned,
    this.clockedInAt,
    this.clockedOutAt,
  });
  
  factory MyShiftDto.fromJson(Map<String, Object?> json) => _$MyShiftDtoFromJson(json);
  
  @JsonKey(name: 'allocation_id')
  final String allocationId;
  @JsonKey(name: 'shift_id')
  final String shiftId;
  @JsonKey(name: 'ward_name')
  final String wardName;
  final DateTime date;
  @JsonKey(name: 'start_time')
  final String startTime;
  @JsonKey(name: 'end_time')
  final String endTime;
  @JsonKey(name: 'crosses_midnight')
  final bool crossesMidnight;
  final AllocationStatus status;
  @JsonKey(name: 'clocked_in_at')
  final DateTime? clockedInAt;
  @JsonKey(name: 'clocked_out_at')
  final DateTime? clockedOutAt;
  @JsonKey(name: 'can_clock_in')
  final bool canClockIn;
  @JsonKey(name: 'was_reassigned')
  final bool wasReassigned;

  Map<String, Object?> toJson() => _$MyShiftDtoToJson(this);
}
