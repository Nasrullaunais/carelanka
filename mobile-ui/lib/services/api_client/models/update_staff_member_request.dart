// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'staff_role.dart';

part 'update_staff_member_request.g.dart';

@JsonSerializable()
class UpdateStaffMemberRequest {
  const UpdateStaffMemberRequest({
    this.firstName,
    this.lastName,
    this.phoneNumber,
    this.role,
    this.department,
  });
  
  factory UpdateStaffMemberRequest.fromJson(Map<String, Object?> json) => _$UpdateStaffMemberRequestFromJson(json);
  
  @JsonKey(name: 'first_name')
  final String? firstName;
  @JsonKey(name: 'last_name')
  final String? lastName;
  @JsonKey(name: 'phone_number')
  final String? phoneNumber;
  final StaffRole? role;
  final String? department;

  Map<String, Object?> toJson() => _$UpdateStaffMemberRequestToJson(this);
}
