// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'ambulance_crew_assignment.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

AmbulanceCrewAssignment _$AmbulanceCrewAssignmentFromJson(
  Map<String, dynamic> json,
) => AmbulanceCrewAssignment(
  id: json['id'] as String?,
  ambulanceId: json['ambulance_id'] as String?,
  staffMemberId: json['staff_member_id'] as String?,
  fullName: json['full_name'] as String?,
  assignedAt: json['assigned_at'] == null
      ? null
      : DateTime.parse(json['assigned_at'] as String),
  assignedByStaffId: json['assigned_by_staff_id'] as String?,
  unassignedAt: json['unassigned_at'] == null
      ? null
      : DateTime.parse(json['unassigned_at'] as String),
  unassignedByStaffId: json['unassigned_by_staff_id'] as String?,
);

Map<String, dynamic> _$AmbulanceCrewAssignmentToJson(
  AmbulanceCrewAssignment instance,
) => <String, dynamic>{
  'id': instance.id,
  'ambulance_id': instance.ambulanceId,
  'staff_member_id': instance.staffMemberId,
  'full_name': instance.fullName,
  'assigned_at': instance.assignedAt?.toIso8601String(),
  'assigned_by_staff_id': instance.assignedByStaffId,
  'unassigned_at': instance.unassignedAt?.toIso8601String(),
  'unassigned_by_staff_id': instance.unassignedByStaffId,
};
