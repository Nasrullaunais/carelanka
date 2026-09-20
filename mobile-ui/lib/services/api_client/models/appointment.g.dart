// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'appointment.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

Appointment _$AppointmentFromJson(Map<String, dynamic> json) => Appointment(
  id: json['id'] as String,
  patient: PatientSummary.fromJson(json['patient'] as Map<String, dynamic>),
  scheduledAt: DateTime.parse(json['scheduled_at'] as String),
  status: AppointmentStatus.fromJson(json['status'] as String),
  canConfirm: json['can_confirm'] as bool,
  canCancel: json['can_cancel'] as bool,
  canComplete: json['can_complete'] as bool,
  reason: json['reason'] as String?,
  bookedByStaffId: json['booked_by_staff_id'] as String?,
  confirmedAt: json['confirmed_at'] == null
      ? null
      : DateTime.parse(json['confirmed_at'] as String),
  confirmedByStaffId: json['confirmed_by_staff_id'] as String?,
  admissionId: json['admission_id'] as String?,
  cancellationReason: json['cancellation_reason'] as String?,
  cancelledByStaffId: json['cancelled_by_staff_id'] as String?,
  createdAt: json['created_at'] == null
      ? null
      : DateTime.parse(json['created_at'] as String),
  updatedAt: json['updated_at'] == null
      ? null
      : DateTime.parse(json['updated_at'] as String),
);

Map<String, dynamic> _$AppointmentToJson(Appointment instance) =>
    <String, dynamic>{
      'id': instance.id,
      'patient': instance.patient,
      'scheduled_at': instance.scheduledAt.toIso8601String(),
      'status': instance.status,
      'reason': instance.reason,
      'booked_by_staff_id': instance.bookedByStaffId,
      'confirmed_at': instance.confirmedAt?.toIso8601String(),
      'confirmed_by_staff_id': instance.confirmedByStaffId,
      'admission_id': instance.admissionId,
      'cancellation_reason': instance.cancellationReason,
      'cancelled_by_staff_id': instance.cancelledByStaffId,
      'can_confirm': instance.canConfirm,
      'can_cancel': instance.canCancel,
      'can_complete': instance.canComplete,
      'created_at': instance.createdAt?.toIso8601String(),
      'updated_at': instance.updatedAt?.toIso8601String(),
    };
