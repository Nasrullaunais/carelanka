// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'create_staff_member_request.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

CreateStaffMemberRequest _$CreateStaffMemberRequestFromJson(
  Map<String, dynamic> json,
) => CreateStaffMemberRequest(
  firstName: json['first_name'] as String,
  lastName: json['last_name'] as String,
  email: json['email'] as String,
  temporaryPassword: json['temporary_password'] as String,
  role: StaffRole.fromJson(json['role'] as String),
  phoneNumber: json['phone_number'] as String?,
  department: json['department'] as String?,
  skillIds: (json['skill_ids'] as List<dynamic>?)
      ?.map((e) => e as String)
      .toList(),
);

Map<String, dynamic> _$CreateStaffMemberRequestToJson(
  CreateStaffMemberRequest instance,
) => <String, dynamic>{
  'first_name': instance.firstName,
  'last_name': instance.lastName,
  'email': instance.email,
  'temporary_password': instance.temporaryPassword,
  'phone_number': instance.phoneNumber,
  'role': instance.role,
  'department': instance.department,
  'skill_ids': instance.skillIds,
};
