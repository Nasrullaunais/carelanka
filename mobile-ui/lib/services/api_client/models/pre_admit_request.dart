// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'admission_urgency.dart';
import 'gender.dart';
import 'ward_type.dart';

part 'pre_admit_request.g.dart';

@JsonSerializable()
class PreAdmitRequest {
  const PreAdmitRequest({
    this.dispatchId,
    this.patientIsCaller,
    this.callerUserId,
    this.patientId,
    this.expectedArrival,
    this.urgency,
    this.destinationWardTypeHint,
    this.provisionalName,
    this.provisionalGender,
  });
  
  factory PreAdmitRequest.fromJson(Map<String, Object?> json) => _$PreAdmitRequestFromJson(json);
  
  @JsonKey(name: 'dispatch_id')
  final String? dispatchId;
  @JsonKey(name: 'patient_is_caller')
  final bool? patientIsCaller;
  @JsonKey(name: 'caller_user_id')
  final String? callerUserId;
  @JsonKey(name: 'patient_id')
  final String? patientId;
  @JsonKey(name: 'expected_arrival')
  final DateTime? expectedArrival;
  final AdmissionUrgency? urgency;
  @JsonKey(name: 'destination_ward_type_hint')
  final WardType? destinationWardTypeHint;
  @JsonKey(name: 'provisional_name')
  final String? provisionalName;
  @JsonKey(name: 'provisional_gender')
  final Gender? provisionalGender;

  Map<String, Object?> toJson() => _$PreAdmitRequestToJson(this);
}
