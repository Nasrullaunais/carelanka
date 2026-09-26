// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'update_staff_member_request.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

UpdateStaffMemberRequest _$UpdateStaffMemberRequestFromJson(
  Map<String, dynamic> json,
) => UpdateStaffMemberRequest(
  firstName: json['first_name'] as String?,
  lastName: json['last_name'] as String?,
  phoneNumber: json['phone_number'] as String?,
  role: json['role'] == null
      ? null
      : StaffRole.fromJson(json['role'] as String),
  department: json['department'] as String?,
);

Map<String, dynamic> _$UpdateStaffMemberRequestToJson(
  UpdateStaffMemberRequest instance,
) => <String, dynamic>{
  'first_name': instance.firstName,
  'last_name': instance.lastName,
  'phone_number': instance.phoneNumber,
  'role': instance.role,
  'department': instance.department,
};
