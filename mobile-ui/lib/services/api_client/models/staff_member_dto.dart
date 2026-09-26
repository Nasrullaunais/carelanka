// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'staff_role.dart';

part 'staff_member_dto.g.dart';

@JsonSerializable()
class StaffMemberDto {
  const StaffMemberDto({
    required this.id,
    required this.employeeNumber,
    required this.firstName,
    required this.lastName,
    required this.fullName,
    required this.email,
    required this.role,
    required this.isActive,
    required this.createdAt,
    required this.updatedAt,
    this.phoneNumber,
    this.department,
  });
  
  factory StaffMemberDto.fromJson(Map<String, Object?> json) => _$StaffMemberDtoFromJson(json);
  
  final String id;
  @JsonKey(name: 'employee_number')
  final String employeeNumber;
  @JsonKey(name: 'first_name')
  final String firstName;
  @JsonKey(name: 'last_name')
  final String lastName;
  @JsonKey(name: 'full_name')
  final String fullName;
  final String email;
  @JsonKey(name: 'phone_number')
  final String? phoneNumber;
  final StaffRole role;
  final String? department;
  @JsonKey(name: 'is_active')
  final bool isActive;
  @JsonKey(name: 'created_at')
  final DateTime createdAt;
  @JsonKey(name: 'updated_at')
  final DateTime updatedAt;

  Map<String, Object?> toJson() => _$StaffMemberDtoToJson(this);
}
