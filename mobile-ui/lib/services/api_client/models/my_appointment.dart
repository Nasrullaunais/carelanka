// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'appointment_status.dart';

part 'my_appointment.g.dart';

@JsonSerializable()
class MyAppointment {
  const MyAppointment({
    required this.appointmentId,
    required this.scheduledAt,
    required this.status,
    required this.statusText,
    required this.canCancel,
    this.reason,
  });
  
  factory MyAppointment.fromJson(Map<String, Object?> json) => _$MyAppointmentFromJson(json);
  
  @JsonKey(name: 'appointment_id')
  final String appointmentId;
  @JsonKey(name: 'scheduled_at')
  final DateTime scheduledAt;
  final AppointmentStatus status;
  @JsonKey(name: 'status_text')
  final String statusText;
  final String? reason;
  @JsonKey(name: 'can_cancel')
  final bool canCancel;

  Map<String, Object?> toJson() => _$MyAppointmentToJson(this);
}
