// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'my_appointment.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

MyAppointment _$MyAppointmentFromJson(Map<String, dynamic> json) =>
    MyAppointment(
      appointmentId: json['appointment_id'] as String,
      scheduledAt: DateTime.parse(json['scheduled_at'] as String),
      status: AppointmentStatus.fromJson(json['status'] as String),
      statusText: json['status_text'] as String,
      canCancel: json['can_cancel'] as bool,
      cancelledByHospital: json['cancelled_by_hospital'] as bool,
      reason: json['reason'] as String?,
      cancellationReason: json['cancellation_reason'] as String?,
    );

Map<String, dynamic> _$MyAppointmentToJson(MyAppointment instance) =>
    <String, dynamic>{
      'appointment_id': instance.appointmentId,
      'scheduled_at': instance.scheduledAt.toIso8601String(),
      'status': instance.status,
      'status_text': instance.statusText,
      'reason': instance.reason,
      'can_cancel': instance.canCancel,
      'cancellation_reason': instance.cancellationReason,
      'cancelled_by_hospital': instance.cancelledByHospital,
    };
