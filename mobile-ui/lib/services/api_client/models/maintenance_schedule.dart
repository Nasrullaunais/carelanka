// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'asset_type.dart';
import 'maintenance_status.dart';
import 'maintenance_type.dart';
import 'raised_by.dart';

part 'maintenance_schedule.g.dart';

@JsonSerializable()
class MaintenanceSchedule {
  const MaintenanceSchedule({
    required this.id,
    required this.assetType,
    required this.assetId,
    required this.assetLabel,
    required this.scheduleType,
    required this.scheduledDate,
    required this.status,
    required this.createdBy,
    required this.createdAt,
    required this.updatedAt,
    this.performedByStaffId,
    this.completedAt,
    this.notes,
  });
  
  factory MaintenanceSchedule.fromJson(Map<String, Object?> json) => _$MaintenanceScheduleFromJson(json);
  
  final String id;
  @JsonKey(name: 'asset_type')
  final AssetType assetType;
  @JsonKey(name: 'asset_id')
  final String assetId;
  @JsonKey(name: 'asset_label')
  final String assetLabel;
  @JsonKey(name: 'schedule_type')
  final MaintenanceType scheduleType;
  @JsonKey(name: 'scheduled_date')
  final DateTime scheduledDate;
  final MaintenanceStatus status;
  @JsonKey(name: 'performed_by_staff_id')
  final String? performedByStaffId;
  @JsonKey(name: 'completed_at')
  final DateTime? completedAt;
  final String? notes;
  @JsonKey(name: 'created_by')
  final RaisedBy createdBy;
  @JsonKey(name: 'created_at')
  final DateTime createdAt;
  @JsonKey(name: 'updated_at')
  final DateTime updatedAt;

  Map<String, Object?> toJson() => _$MaintenanceScheduleToJson(this);
}
