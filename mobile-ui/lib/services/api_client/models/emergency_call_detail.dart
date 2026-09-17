// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'call_priority.dart';
import 'call_status.dart';
import 'cancellation_request_status.dart';
import 'dispatch_summary.dart';

part 'emergency_call_detail.g.dart';

@JsonSerializable()
class EmergencyCallDetail {
  const EmergencyCallDetail({
    this.id,
    this.patientId,
    this.callerUserId,
    this.patientIsCaller,
    this.callerName,
    this.callerPhone,
    this.latitude,
    this.longitude,
    this.locationAccuracyMetres,
    this.locationCapturedAt,
    this.idempotencyKey,
    this.addressLabel,
    this.details,
    this.priority,
    this.status,
    this.outcome,
    this.transported,
    this.cancellationRequestStatus,
    this.createdAt,
    this.updatedAt,
    this.dispatches,
    this.openProposalId,
  });

  factory EmergencyCallDetail.fromJson(Map<String, Object?> json) => _$EmergencyCallDetailFromJson(json);

  final String? id;
  @JsonKey(name: 'patient_id')
  final String? patientId;
  @JsonKey(name: 'caller_user_id')
  final String? callerUserId;
  @JsonKey(name: 'patient_is_caller')
  final bool? patientIsCaller;
  @JsonKey(name: 'caller_name')
  final String? callerName;
  @JsonKey(name: 'caller_phone')
  final String? callerPhone;
  final double? latitude;
  final double? longitude;
  @JsonKey(name: 'location_accuracy_metres')
  final double? locationAccuracyMetres;
  @JsonKey(name: 'location_captured_at')
  final DateTime? locationCapturedAt;
  @JsonKey(name: 'idempotency_key')
  final String? idempotencyKey;
  @JsonKey(name: 'address_label')
  final String? addressLabel;
  final String? details;
  final CallPriority? priority;
  final CallStatus? status;
  final String? outcome;
  final bool? transported;
  @JsonKey(name: 'cancellation_request_status')
  final CancellationRequestStatus? cancellationRequestStatus;
  @JsonKey(name: 'created_at')
  final DateTime? createdAt;
  @JsonKey(name: 'updated_at')
  final DateTime? updatedAt;
  final List<DispatchSummary>? dispatches;
  @JsonKey(name: 'open_proposal_id')
  final String? openProposalId;

  Map<String, Object?> toJson() => _$EmergencyCallDetailToJson(this);
}
