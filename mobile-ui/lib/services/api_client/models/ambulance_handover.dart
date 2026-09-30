// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'ambulance_handover.g.dart';

@JsonSerializable()
class AmbulanceHandover {
  const AmbulanceHandover({
    this.ambulanceRegistration,
    this.handedOverAt,
    this.patientCondition,
    this.notes,
  });
  
  factory AmbulanceHandover.fromJson(Map<String, Object?> json) => _$AmbulanceHandoverFromJson(json);
  
  @JsonKey(name: 'ambulance_registration')
  final String? ambulanceRegistration;
  @JsonKey(name: 'handed_over_at')
  final DateTime? handedOverAt;
  @JsonKey(name: 'patient_condition')
  final String? patientCondition;
  final String? notes;

  Map<String, Object?> toJson() => _$AmbulanceHandoverToJson(this);
}
