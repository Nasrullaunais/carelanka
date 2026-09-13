// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'create_appointment_request.g.dart';

@JsonSerializable()
class CreateAppointmentRequest {
  const CreateAppointmentRequest({
    required this.patientId,
    required this.scheduledAt,
    this.reason,
  });
  
  factory CreateAppointmentRequest.fromJson(Map<String, Object?> json) => _$CreateAppointmentRequestFromJson(json);
  
  @JsonKey(name: 'patient_id')
  final String patientId;
  @JsonKey(name: 'scheduled_at')
  final DateTime scheduledAt;
  final String? reason;

  Map<String, Object?> toJson() => _$CreateAppointmentRequestToJson(this);
}
