// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'patient_login_request.g.dart';

@JsonSerializable()
class PatientLoginRequest {
  const PatientLoginRequest({
    required this.username,
    required this.password,
  });
  
  factory PatientLoginRequest.fromJson(Map<String, Object?> json) => _$PatientLoginRequestFromJson(json);
  
  final String username;
  final String password;

  Map<String, Object?> toJson() => _$PatientLoginRequestToJson(this);
}
