// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'complete_maintenance_schedule_request.g.dart';

@JsonSerializable()
class CompleteMaintenanceScheduleRequest {
  const CompleteMaintenanceScheduleRequest({
    this.notes,
  });
  
  factory CompleteMaintenanceScheduleRequest.fromJson(Map<String, Object?> json) => _$CompleteMaintenanceScheduleRequestFromJson(json);
  
  final String? notes;

  Map<String, Object?> toJson() => _$CompleteMaintenanceScheduleRequestToJson(this);
}
