// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'call_priority.dart';

part 'update_emergency_call_request.g.dart';

@JsonSerializable()
class UpdateEmergencyCallRequest {
  const UpdateEmergencyCallRequest({
    this.priority,
    this.details,
    this.callerName,
    this.callerPhone,
    this.latitude,
    this.longitude,
  });

  factory UpdateEmergencyCallRequest.fromJson(Map<String, Object?> json) => _$UpdateEmergencyCallRequestFromJson(json);

  final CallPriority? priority;
  final String? details;
  @JsonKey(name: 'caller_name')
  final String? callerName;
  @JsonKey(name: 'caller_phone')
  final String? callerPhone;
  final double? latitude;
  final double? longitude;

  Map<String, Object?> toJson() => _$UpdateEmergencyCallRequestToJson(this);
}
