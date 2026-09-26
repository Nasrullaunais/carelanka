// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'create_walk_in_appointment_request.g.dart';

@JsonSerializable()
class CreateWalkInAppointmentRequest {
  const CreateWalkInAppointmentRequest({
    required this.patientId,
    this.reason,
  });
  
  factory CreateWalkInAppointmentRequest.fromJson(Map<String, Object?> json) => _$CreateWalkInAppointmentRequestFromJson(json);
  
  @JsonKey(name: 'patient_id')
  final String patientId;
  final String? reason;

  Map<String, Object?> toJson() => _$CreateWalkInAppointmentRequestToJson(this);
}
