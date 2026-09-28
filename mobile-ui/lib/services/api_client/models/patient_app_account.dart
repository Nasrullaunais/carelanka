// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'patient_app_account.g.dart';

@JsonSerializable()
class PatientAppAccount {
  const PatientAppAccount({
    required this.patientId,
    required this.patientCode,
    required this.fullName,
    required this.username,
    this.nic,
    this.dateOfBirth,
  });
  
  factory PatientAppAccount.fromJson(Map<String, Object?> json) => _$PatientAppAccountFromJson(json);
  
  @JsonKey(name: 'patient_id')
  final String patientId;
  @JsonKey(name: 'patient_code')
  final String patientCode;
  @JsonKey(name: 'full_name')
  final String fullName;
  final String? nic;
  @JsonKey(name: 'date_of_birth')
  final DateTime? dateOfBirth;
  final String username;

  Map<String, Object?> toJson() => _$PatientAppAccountToJson(this);
}
