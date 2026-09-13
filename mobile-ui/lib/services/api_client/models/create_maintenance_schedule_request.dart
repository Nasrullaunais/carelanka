// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'asset_type.dart';
import 'maintenance_type.dart';

part 'create_maintenance_schedule_request.g.dart';

@JsonSerializable()
class CreateMaintenanceScheduleRequest {
  const CreateMaintenanceScheduleRequest({
    required this.assetType,
    required this.assetId,
    required this.scheduleType,
    required this.scheduledDate,
    this.notes,
  });
  
  factory CreateMaintenanceScheduleRequest.fromJson(Map<String, Object?> json) => _$CreateMaintenanceScheduleRequestFromJson(json);
  
  @JsonKey(name: 'asset_type')
  final AssetType assetType;
  @JsonKey(name: 'asset_id')
  final String assetId;
  @JsonKey(name: 'schedule_type')
  final MaintenanceType scheduleType;
  @JsonKey(name: 'scheduled_date')
  final DateTime scheduledDate;
  final String? notes;

  Map<String, Object?> toJson() => _$CreateMaintenanceScheduleRequestToJson(this);
}
