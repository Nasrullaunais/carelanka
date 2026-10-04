// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'call_priority.dart';
import 'call_status.dart';

part 'fleet_map_call.g.dart';

@JsonSerializable()
class FleetMapCall {
  const FleetMapCall({
    this.id,
    this.priority,
    this.status,
    this.addressLabel,
    this.latitude,
    this.longitude,
    this.waitingMinutes,
    this.assignedAmbulanceId,
    this.createdAt,
  });
  
  factory FleetMapCall.fromJson(Map<String, Object?> json) => _$FleetMapCallFromJson(json);
  
  final String? id;
  final CallPriority? priority;
  final CallStatus? status;
  @JsonKey(name: 'address_label')
  final String? addressLabel;
  final double? latitude;
  final double? longitude;
  @JsonKey(name: 'waiting_minutes')
  final int? waitingMinutes;
  @JsonKey(name: 'assigned_ambulance_id')
  final String? assignedAmbulanceId;
  @JsonKey(name: 'created_at')
  final DateTime? createdAt;

  Map<String, Object?> toJson() => _$FleetMapCallToJson(this);
}
