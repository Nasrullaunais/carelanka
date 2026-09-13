// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'patient_login_request.g.dart';

@JsonSerializable()
class PatientLoginRequest {
  const PatientLoginRequest({
    required this.phoneNumber,
    required this.password,
  });
  
  factory PatientLoginRequest.fromJson(Map<String, Object?> json) => _$PatientLoginRequestFromJson(json);
  
  @JsonKey(name: 'phone_number')
  final String phoneNumber;
  final String password;

  Map<String, Object?> toJson() => _$PatientLoginRequestToJson(this);
}
