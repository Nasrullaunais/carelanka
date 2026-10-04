// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'emergency_call_outcome.dart';

part 'cancel_emergency_call_request.g.dart';

@JsonSerializable()
class CancelEmergencyCallRequest {
  const CancelEmergencyCallRequest({
    this.outcome,
    this.notes,
  });
  
  factory CancelEmergencyCallRequest.fromJson(Map<String, Object?> json) => _$CancelEmergencyCallRequestFromJson(json);
  
  final EmergencyCallOutcome? outcome;
  final String? notes;

  Map<String, Object?> toJson() => _$CancelEmergencyCallRequestToJson(this);
}
