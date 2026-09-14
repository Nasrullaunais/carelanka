// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'patient_register_request.g.dart';

@JsonSerializable()
class PatientRegisterRequest {
  const PatientRegisterRequest({
    required this.username,
    required this.password,
  });
  
  factory PatientRegisterRequest.fromJson(Map<String, Object?> json) => _$PatientRegisterRequestFromJson(json);
  
  final String username;
  final String password;

  Map<String, Object?> toJson() => _$PatientRegisterRequestToJson(this);
}
