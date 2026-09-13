// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'complete_details_request.g.dart';

@JsonSerializable()
class CompleteDetailsRequest {
  const CompleteDetailsRequest({
    this.nic,
    this.fullName,
    this.dateOfBirth,
    this.phone,
    this.address,
    this.emergencyContactName,
    this.emergencyContactPhone,
  });
  
  factory CompleteDetailsRequest.fromJson(Map<String, Object?> json) => _$CompleteDetailsRequestFromJson(json);
  
  final String? nic;
  @JsonKey(name: 'full_name')
  final String? fullName;
  @JsonKey(name: 'date_of_birth')
  final DateTime? dateOfBirth;
  final String? phone;
  final String? address;
  @JsonKey(name: 'emergency_contact_name')
  final String? emergencyContactName;
  @JsonKey(name: 'emergency_contact_phone')
  final String? emergencyContactPhone;

  Map<String, Object?> toJson() => _$CompleteDetailsRequestToJson(this);
}
