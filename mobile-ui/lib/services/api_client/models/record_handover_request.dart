// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'record_handover_request.g.dart';

@JsonSerializable()
class RecordHandoverRequest {
  const RecordHandoverRequest({
    this.notes,
    this.patientCondition,
  });
  
  factory RecordHandoverRequest.fromJson(Map<String, Object?> json) => _$RecordHandoverRequestFromJson(json);
  
  final String? notes;
  @JsonKey(name: 'patient_condition')
  final String? patientCondition;

  Map<String, Object?> toJson() => _$RecordHandoverRequestToJson(this);
}
