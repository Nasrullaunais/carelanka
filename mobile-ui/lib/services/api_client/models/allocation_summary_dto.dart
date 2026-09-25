// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'allocation_status.dart';

part 'allocation_summary_dto.g.dart';

@JsonSerializable()
class AllocationSummaryDto {
  const AllocationSummaryDto({
    required this.allocationId,
    required this.shiftId,
    required this.wardName,
    required this.date,
    required this.startTime,
    required this.endTime,
    required this.staffMemberId,
    required this.staffName,
    required this.status,
  });
  
  factory AllocationSummaryDto.fromJson(Map<String, Object?> json) => _$AllocationSummaryDtoFromJson(json);
  
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
  @JsonKey(name: 'staff_member_id')
  final String staffMemberId;
  @JsonKey(name: 'staff_name')
  final String staffName;
  final AllocationStatus status;

  Map<String, Object?> toJson() => _$AllocationSummaryDtoToJson(this);
}
