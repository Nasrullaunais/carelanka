// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'patient_register_request.g.dart';

@JsonSerializable()
class PatientRegisterRequest {
  const PatientRegisterRequest({
    required this.phoneNumber,
    required this.password,
    required this.fullName,
  });
  
  factory PatientRegisterRequest.fromJson(Map<String, Object?> json) => _$PatientRegisterRequestFromJson(json);
  
  @JsonKey(name: 'phone_number')
  final String phoneNumber;
  final String password;
  @JsonKey(name: 'full_name')
  final String fullName;

  Map<String, Object?> toJson() => _$PatientRegisterRequestToJson(this);
}
