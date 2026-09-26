// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'staff_role.dart';

part 'create_staff_member_request.g.dart';

@JsonSerializable()
class CreateStaffMemberRequest {
  const CreateStaffMemberRequest({
    required this.firstName,
    required this.lastName,
    required this.email,
    required this.temporaryPassword,
    required this.role,
    this.phoneNumber,
    this.department,
    this.skillIds,
  });
  
  factory CreateStaffMemberRequest.fromJson(Map<String, Object?> json) => _$CreateStaffMemberRequestFromJson(json);
  
  @JsonKey(name: 'first_name')
  final String firstName;
  @JsonKey(name: 'last_name')
  final String lastName;
  final String email;
  @JsonKey(name: 'temporary_password')
  final String temporaryPassword;
  @JsonKey(name: 'phone_number')
  final String? phoneNumber;
  final StaffRole role;
  final String? department;
  @JsonKey(name: 'skill_ids')
  final List<String>? skillIds;

  Map<String, Object?> toJson() => _$CreateStaffMemberRequestToJson(this);
}
