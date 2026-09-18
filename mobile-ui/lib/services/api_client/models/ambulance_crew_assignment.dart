// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'ambulance_crew_assignment.g.dart';

@JsonSerializable()
class AmbulanceCrewAssignment {
  const AmbulanceCrewAssignment({
    this.id,
    this.ambulanceId,
    this.staffMemberId,
    this.fullName,
    this.assignedAt,
    this.assignedByStaffId,
    this.unassignedAt,
    this.unassignedByStaffId,
  });
  
  factory AmbulanceCrewAssignment.fromJson(Map<String, Object?> json) => _$AmbulanceCrewAssignmentFromJson(json);
  
  final String? id;
  @JsonKey(name: 'ambulance_id')
  final String? ambulanceId;
  @JsonKey(name: 'staff_member_id')
  final String? staffMemberId;
  @JsonKey(name: 'full_name')
  final String? fullName;
  @JsonKey(name: 'assigned_at')
  final DateTime? assignedAt;
  @JsonKey(name: 'assigned_by_staff_id')
  final String? assignedByStaffId;
  @JsonKey(name: 'unassigned_at')
  final DateTime? unassignedAt;
  @JsonKey(name: 'unassigned_by_staff_id')
  final String? unassignedByStaffId;

  Map<String, Object?> toJson() => _$AmbulanceCrewAssignmentToJson(this);
}
