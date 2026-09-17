// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'cancellation_request_status.dart';

part 'emergency_cancellation_request.g.dart';

@JsonSerializable()
class EmergencyCancellationRequest {
  const EmergencyCancellationRequest({
    this.emergencyCallId,
    this.status,
    this.reason,
    this.requestedAt,
    this.reviewedAt,
    this.reviewedByStaffId,
    this.reviewNotes,
  });

  factory EmergencyCancellationRequest.fromJson(Map<String, Object?> json) => _$EmergencyCancellationRequestFromJson(json);

  @JsonKey(name: 'emergency_call_id')
  final String? emergencyCallId;
  final CancellationRequestStatus? status;
  final String? reason;
  @JsonKey(name: 'requested_at')
  final DateTime? requestedAt;
  @JsonKey(name: 'reviewed_at')
  final DateTime? reviewedAt;
  @JsonKey(name: 'reviewed_by_staff_id')
  final String? reviewedByStaffId;
  @JsonKey(name: 'review_notes')
  final String? reviewNotes;

  Map<String, Object?> toJson() => _$EmergencyCancellationRequestToJson(this);
}
