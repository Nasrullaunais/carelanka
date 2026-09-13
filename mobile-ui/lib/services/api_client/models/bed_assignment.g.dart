// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'bed_assignment.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

BedAssignment _$BedAssignmentFromJson(Map<String, dynamic> json) =>
    BedAssignment(
      id: json['id'] as String,
      admissionId: json['admission_id'] as String,
      bedId: json['bed_id'] as String,
      wardName: json['ward_name'] as String,
      bedNumber: json['bed_number'] as String,
      status: AssignmentStatus.fromJson(json['status'] as String),
      assignedBy: AssignedBy.fromJson(json['assigned_by'] as String),
      isDowngrade: json['is_downgrade'] as bool,
      reservedUntil: json['reserved_until'] == null
          ? null
          : DateTime.parse(json['reserved_until'] as String),
      workflowId: json['workflow_id'] as String?,
      approvedByStaffId: json['approved_by_staff_id'] as String?,
      approvedByStaffName: json['approved_by_staff_name'] as String?,
      approvedAt: json['approved_at'] == null
          ? null
          : DateTime.parse(json['approved_at'] as String),
      overrideReason: json['override_reason'] as String?,
      releasedAt: json['released_at'] == null
          ? null
          : DateTime.parse(json['released_at'] as String),
      releaseReason: json['release_reason'] == null
          ? null
          : ReleaseReason.fromJson(json['release_reason'] as String),
      createdAt: json['created_at'] == null
          ? null
          : DateTime.parse(json['created_at'] as String),
      updatedAt: json['updated_at'] == null
          ? null
          : DateTime.parse(json['updated_at'] as String),
    );

Map<String, dynamic> _$BedAssignmentToJson(BedAssignment instance) =>
    <String, dynamic>{
      'id': instance.id,
      'admission_id': instance.admissionId,
      'bed_id': instance.bedId,
      'ward_name': instance.wardName,
      'bed_number': instance.bedNumber,
      'status': instance.status,
      'reserved_until': instance.reservedUntil?.toIso8601String(),
      'assigned_by': instance.assignedBy,
      'workflow_id': instance.workflowId,
      'is_downgrade': instance.isDowngrade,
      'approved_by_staff_id': instance.approvedByStaffId,
      'approved_by_staff_name': instance.approvedByStaffName,
      'approved_at': instance.approvedAt?.toIso8601String(),
      'override_reason': instance.overrideReason,
      'released_at': instance.releasedAt?.toIso8601String(),
      'release_reason': instance.releaseReason,
      'created_at': instance.createdAt?.toIso8601String(),
      'updated_at': instance.updatedAt?.toIso8601String(),
    };
