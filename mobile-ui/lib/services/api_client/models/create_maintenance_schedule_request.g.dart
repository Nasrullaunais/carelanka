// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'create_maintenance_schedule_request.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

CreateMaintenanceScheduleRequest _$CreateMaintenanceScheduleRequestFromJson(
  Map<String, dynamic> json,
) => CreateMaintenanceScheduleRequest(
  assetType: AssetType.fromJson(json['asset_type'] as String),
  assetId: json['asset_id'] as String,
  scheduleType: MaintenanceType.fromJson(json['schedule_type'] as String),
  scheduledDate: DateTime.parse(json['scheduled_date'] as String),
  notes: json['notes'] as String?,
);

Map<String, dynamic> _$CreateMaintenanceScheduleRequestToJson(
  CreateMaintenanceScheduleRequest instance,
) => <String, dynamic>{
  'asset_type': instance.assetType,
  'asset_id': instance.assetId,
  'schedule_type': instance.scheduleType,
  'scheduled_date': instance.scheduledDate.toIso8601String(),
  'notes': instance.notes,
};
