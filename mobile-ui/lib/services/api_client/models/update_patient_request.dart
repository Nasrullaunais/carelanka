// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'gender.dart';

part 'update_patient_request.g.dart';

@JsonSerializable()
class UpdatePatientRequest {
  const UpdatePatientRequest({
    required this.fullName,
    required this.gender,
    this.nic,
    this.dateOfBirth,
    this.phone,
    this.address,
    this.emergencyContactName,
    this.emergencyContactPhone,
  });
  
  factory UpdatePatientRequest.fromJson(Map<String, Object?> json) => _$UpdatePatientRequestFromJson(json);
  
  @JsonKey(name: 'full_name')
  final String fullName;
  final String? nic;
  final Gender gender;
  @JsonKey(name: 'date_of_birth')
  final DateTime? dateOfBirth;
  final String? phone;
  final String? address;
  @JsonKey(name: 'emergency_contact_name')
  final String? emergencyContactName;
  @JsonKey(name: 'emergency_contact_phone')
  final String? emergencyContactPhone;

  Map<String, Object?> toJson() => _$UpdatePatientRequestToJson(this);
}
