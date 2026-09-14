// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'call_priority.dart';

part 'create_emergency_call_request.g.dart';

@JsonSerializable()
class CreateEmergencyCallRequest {
  const CreateEmergencyCallRequest({
    required this.patientIsCaller,
    required this.latitude,
    required this.longitude,
    required this.locationAccuracyMetres,
    required this.locationCapturedAt,
    required this.idempotencyKey,
    this.patientId,
    this.callerName,
    this.callerPhone,
    this.details,
    this.priority,
  });

  factory CreateEmergencyCallRequest.fromJson(Map<String, Object?> json) => _$CreateEmergencyCallRequestFromJson(json);

  @JsonKey(name: 'patient_is_caller')
  final bool patientIsCaller;
  @JsonKey(name: 'patient_id')
  final String? patientId;
  @JsonKey(name: 'caller_name')
  final String? callerName;
  @JsonKey(name: 'caller_phone')
  final String? callerPhone;
  final double latitude;
  final double longitude;
  @JsonKey(name: 'location_accuracy_metres')
  final double locationAccuracyMetres;
  @JsonKey(name: 'location_captured_at')
  final DateTime locationCapturedAt;
  @JsonKey(name: 'idempotency_key')
  final String idempotencyKey;
  final String? details;
  final CallPriority? priority;

  Map<String, Object?> toJson() => _$CreateEmergencyCallRequestToJson(this);
}
