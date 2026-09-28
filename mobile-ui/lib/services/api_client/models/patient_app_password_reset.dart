// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'patient_app_password_reset.g.dart';

@JsonSerializable()
class PatientAppPasswordReset {
  const PatientAppPasswordReset({
    required this.username,
    required this.temporaryPassword,
  });
  
  factory PatientAppPasswordReset.fromJson(Map<String, Object?> json) => _$PatientAppPasswordResetFromJson(json);
  
  final String username;
  @JsonKey(name: 'temporary_password')
  final String temporaryPassword;

  Map<String, Object?> toJson() => _$PatientAppPasswordResetToJson(this);
}
