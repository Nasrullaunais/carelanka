// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'appointment_status.dart';
import 'patient_summary.dart';

part 'appointment.g.dart';

@JsonSerializable()
class Appointment {
  const Appointment({
    required this.id,
    required this.patient,
    required this.scheduledAt,
    required this.status,
    required this.canConfirm,
    required this.canCancel,
    required this.canComplete,
    this.reason,
    this.bookedByStaffId,
    this.confirmedAt,
    this.confirmedByStaffId,
    this.admissionId,
    this.cancellationReason,
    this.cancelledByStaffId,
    this.createdAt,
    this.updatedAt,
  });
  
  factory Appointment.fromJson(Map<String, Object?> json) => _$AppointmentFromJson(json);
  
  final String id;
  final PatientSummary patient;
  @JsonKey(name: 'scheduled_at')
  final DateTime scheduledAt;
  final AppointmentStatus status;
  final String? reason;
  @JsonKey(name: 'booked_by_staff_id')
  final String? bookedByStaffId;
  @JsonKey(name: 'confirmed_at')
  final DateTime? confirmedAt;
  @JsonKey(name: 'confirmed_by_staff_id')
  final String? confirmedByStaffId;
  @JsonKey(name: 'admission_id')
  final String? admissionId;
  @JsonKey(name: 'cancellation_reason')
  final String? cancellationReason;
  @JsonKey(name: 'cancelled_by_staff_id')
  final String? cancelledByStaffId;
  @JsonKey(name: 'can_confirm')
  final bool canConfirm;
  @JsonKey(name: 'can_cancel')
  final bool canCancel;
  @JsonKey(name: 'can_complete')
  final bool canComplete;
  @JsonKey(name: 'created_at')
  final DateTime? createdAt;
  @JsonKey(name: 'updated_at')
  final DateTime? updatedAt;

  Map<String, Object?> toJson() => _$AppointmentToJson(this);
}
