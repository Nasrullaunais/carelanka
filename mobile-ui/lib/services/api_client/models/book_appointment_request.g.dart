// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'book_appointment_request.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

BookAppointmentRequest _$BookAppointmentRequestFromJson(
  Map<String, dynamic> json,
) => BookAppointmentRequest(
  scheduledAt: DateTime.parse(json['scheduled_at'] as String),
  reason: json['reason'] as String?,
);

Map<String, dynamic> _$BookAppointmentRequestToJson(
  BookAppointmentRequest instance,
) => <String, dynamic>{
  'scheduled_at': instance.scheduledAt.toIso8601String(),
  'reason': instance.reason,
};
