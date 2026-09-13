// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'gender.dart';

part 'patient.g.dart';

@JsonSerializable()
class Patient {
  const Patient({
    required this.id,
    required this.patientCode,
    required this.fullName,
    required this.gender,
    required this.hasAccount,
    this.nic,
    this.tempReference,
    this.dateOfBirth,
    this.phone,
    this.address,
    this.emergencyContactName,
    this.emergencyContactPhone,
    this.createdAt,
    this.updatedAt,
  });
  
  factory Patient.fromJson(Map<String, Object?> json) => _$PatientFromJson(json);
  
  final String id;
  @JsonKey(name: 'patient_code')
  final String patientCode;
  @JsonKey(name: 'full_name')
  final String fullName;
  final String? nic;
  @JsonKey(name: 'temp_reference')
  final String? tempReference;
  final Gender gender;
  @JsonKey(name: 'date_of_birth')
  final DateTime? dateOfBirth;
  final String? phone;
  final String? address;
  @JsonKey(name: 'emergency_contact_name')
  final String? emergencyContactName;
  @JsonKey(name: 'emergency_contact_phone')
  final String? emergencyContactPhone;
  @JsonKey(name: 'has_account')
  final bool hasAccount;
  @JsonKey(name: 'created_at')
  final DateTime? createdAt;
  @JsonKey(name: 'updated_at')
  final DateTime? updatedAt;

  Map<String, Object?> toJson() => _$PatientToJson(this);
}
