// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'bed_occupancy_status.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

BedOccupancyStatus _$BedOccupancyStatusFromJson(Map<String, dynamic> json) =>
    BedOccupancyStatus(
      bedId: json['bed_id'] as String,
      occupied: json['occupied'] as bool,
      mayTakeOutOfService: json['may_take_out_of_service'] as bool,
      assignmentStatus: json['assignment_status'] == null
          ? null
          : AssignmentStatus.fromJson(json['assignment_status'] as String),
      reservedUntil: json['reserved_until'] == null
          ? null
          : DateTime.parse(json['reserved_until'] as String),
    );

Map<String, dynamic> _$BedOccupancyStatusToJson(BedOccupancyStatus instance) =>
    <String, dynamic>{
      'bed_id': instance.bedId,
      'occupied': instance.occupied,
      'assignment_status': instance.assignmentStatus,
      'reserved_until': instance.reservedUntil?.toIso8601String(),
      'may_take_out_of_service': instance.mayTakeOutOfService,
    };
