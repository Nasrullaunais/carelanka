// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'assigned_by.dart';
import 'assignment_status.dart';
import 'release_reason.dart';

part 'bed_assignment.g.dart';

@JsonSerializable()
class BedAssignment {
  const BedAssignment({
    required this.id,
    required this.admissionId,
    required this.bedId,
    required this.wardName,
    required this.bedNumber,
    required this.status,
    required this.assignedBy,
    required this.isDowngrade,
    this.reservedUntil,
    this.workflowId,
    this.approvedByStaffId,
    this.approvedByStaffName,
    this.approvedAt,
    this.overrideReason,
    this.releasedAt,
    this.releaseReason,
    this.createdAt,
    this.updatedAt,
  });
  
  factory BedAssignment.fromJson(Map<String, Object?> json) => _$BedAssignmentFromJson(json);
  
  final String id;
  @JsonKey(name: 'admission_id')
  final String admissionId;
  @JsonKey(name: 'bed_id')
  final String bedId;
  @JsonKey(name: 'ward_name')
  final String wardName;
  @JsonKey(name: 'bed_number')
  final String bedNumber;
  final AssignmentStatus status;
  @JsonKey(name: 'reserved_until')
  final DateTime? reservedUntil;
  @JsonKey(name: 'assigned_by')
  final AssignedBy assignedBy;
  @JsonKey(name: 'workflow_id')
  final String? workflowId;
  @JsonKey(name: 'is_downgrade')
  final bool isDowngrade;
  @JsonKey(name: 'approved_by_staff_id')
  final String? approvedByStaffId;
  @JsonKey(name: 'approved_by_staff_name')
  final String? approvedByStaffName;
  @JsonKey(name: 'approved_at')
  final DateTime? approvedAt;
  @JsonKey(name: 'override_reason')
  final String? overrideReason;
  @JsonKey(name: 'released_at')
  final DateTime? releasedAt;
  @JsonKey(name: 'release_reason')
  final ReleaseReason? releaseReason;
  @JsonKey(name: 'created_at')
  final DateTime? createdAt;
  @JsonKey(name: 'updated_at')
  final DateTime? updatedAt;

  Map<String, Object?> toJson() => _$BedAssignmentToJson(this);
}
