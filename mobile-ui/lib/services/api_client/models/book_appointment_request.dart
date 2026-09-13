// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'book_appointment_request.g.dart';

@JsonSerializable()
class BookAppointmentRequest {
  const BookAppointmentRequest({
    required this.scheduledAt,
    this.reason,
  });
  
  factory BookAppointmentRequest.fromJson(Map<String, Object?> json) => _$BookAppointmentRequestFromJson(json);
  
  @JsonKey(name: 'scheduled_at')
  final DateTime scheduledAt;
  final String? reason;

  Map<String, Object?> toJson() => _$BookAppointmentRequestToJson(this);
}
