// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'maintenance_schedule.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

MaintenanceSchedule _$MaintenanceScheduleFromJson(Map<String, dynamic> json) =>
    MaintenanceSchedule(
      id: json['id'] as String,
      assetType: AssetType.fromJson(json['asset_type'] as String),
      assetId: json['asset_id'] as String,
      assetLabel: json['asset_label'] as String,
      scheduleType: MaintenanceType.fromJson(json['schedule_type'] as String),
      scheduledDate: DateTime.parse(json['scheduled_date'] as String),
      status: MaintenanceStatus.fromJson(json['status'] as String),
      createdBy: RaisedBy.fromJson(json['created_by'] as String),
      createdAt: DateTime.parse(json['created_at'] as String),
      updatedAt: DateTime.parse(json['updated_at'] as String),
      performedByStaffId: json['performed_by_staff_id'] as String?,
      completedAt: json['completed_at'] == null
          ? null
          : DateTime.parse(json['completed_at'] as String),
      notes: json['notes'] as String?,
    );

Map<String, dynamic> _$MaintenanceScheduleToJson(
  MaintenanceSchedule instance,
) => <String, dynamic>{
  'id': instance.id,
  'asset_type': instance.assetType,
  'asset_id': instance.assetId,
  'asset_label': instance.assetLabel,
  'schedule_type': instance.scheduleType,
  'scheduled_date': instance.scheduledDate.toIso8601String(),
  'status': instance.status,
  'performed_by_staff_id': instance.performedByStaffId,
  'completed_at': instance.completedAt?.toIso8601String(),
  'notes': instance.notes,
  'created_by': instance.createdBy,
  'created_at': instance.createdAt.toIso8601String(),
  'updated_at': instance.updatedAt.toIso8601String(),
};
