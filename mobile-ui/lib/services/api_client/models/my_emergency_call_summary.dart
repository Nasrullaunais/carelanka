// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'call_priority.dart';
import 'call_status.dart';
import 'cancellation_request_status.dart';

part 'my_emergency_call_summary.g.dart';

@JsonSerializable()
class MyEmergencyCallSummary {
  const MyEmergencyCallSummary({
    this.id,
    this.patientIsCaller,
    this.priority,
    this.status,
    this.cancellationRequestStatus,
    this.createdAt,
  });
  
  factory MyEmergencyCallSummary.fromJson(Map<String, Object?> json) => _$MyEmergencyCallSummaryFromJson(json);
  
  final String? id;
  @JsonKey(name: 'patient_is_caller')
  final bool? patientIsCaller;
  final CallPriority? priority;
  final CallStatus? status;
  @JsonKey(name: 'cancellation_request_status')
  final CancellationRequestStatus? cancellationRequestStatus;
  @JsonKey(name: 'created_at')
  final DateTime? createdAt;

  Map<String, Object?> toJson() => _$MyEmergencyCallSummaryToJson(this);
}
