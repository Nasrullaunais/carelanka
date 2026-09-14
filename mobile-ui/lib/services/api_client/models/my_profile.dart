// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'gender.dart';

part 'my_profile.g.dart';

@JsonSerializable()
class MyProfile {
  const MyProfile({
    required this.patientCode,
    required this.fullName,
    required this.gender,
    required this.detailsComplete,
    required this.missingFields,
    this.nic,
    this.dateOfBirth,
    this.phone,
    this.address,
    this.emergencyContactName,
    this.emergencyContactPhone,
  });
  
  factory MyProfile.fromJson(Map<String, Object?> json) => _$MyProfileFromJson(json);
  
  @JsonKey(name: 'patient_code')
  final String patientCode;
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
  @JsonKey(name: 'details_complete')
  final bool detailsComplete;
  @JsonKey(name: 'missing_fields')
  final List<String> missingFields;

  Map<String, Object?> toJson() => _$MyProfileToJson(this);
}
