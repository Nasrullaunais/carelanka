// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'assignment_status.dart';

part 'bed_occupancy_status.g.dart';

@JsonSerializable()
class BedOccupancyStatus {
  const BedOccupancyStatus({
    required this.bedId,
    required this.occupied,
    required this.mayTakeOutOfService,
    this.assignmentStatus,
    this.reservedUntil,
  });
  
  factory BedOccupancyStatus.fromJson(Map<String, Object?> json) => _$BedOccupancyStatusFromJson(json);
  
  @JsonKey(name: 'bed_id')
  final String bedId;
  final bool occupied;
  @JsonKey(name: 'assignment_status')
  final AssignmentStatus? assignmentStatus;
  @JsonKey(name: 'reserved_until')
  final DateTime? reservedUntil;
  @JsonKey(name: 'may_take_out_of_service')
  final bool mayTakeOutOfService;

  Map<String, Object?> toJson() => _$BedOccupancyStatusToJson(this);
}
