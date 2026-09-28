// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'staff_member_dto.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

StaffMemberDto _$StaffMemberDtoFromJson(Map<String, dynamic> json) =>
    StaffMemberDto(
      id: json['id'] as String,
      employeeNumber: json['employee_number'] as String,
      firstName: json['first_name'] as String,
      lastName: json['last_name'] as String,
      fullName: json['full_name'] as String,
      email: json['email'] as String,
      role: StaffRole.fromJson(json['role'] as String),
      isActive: json['is_active'] as bool,
      createdAt: DateTime.parse(json['created_at'] as String),
      updatedAt: DateTime.parse(json['updated_at'] as String),
      phoneNumber: json['phone_number'] as String?,
      department: json['department'] as String?,
      title: json['title'] == null
          ? null
          : PersonTitle.fromJson(json['title'] as String),
      specialization: json['specialization'] as String?,
      registrationNumber: json['registration_number'] as String?,
      joiningDate: json['joining_date'] == null
          ? null
          : DateTime.parse(json['joining_date'] as String),
    );

Map<String, dynamic> _$StaffMemberDtoToJson(StaffMemberDto instance) =>
    <String, dynamic>{
      'id': instance.id,
      'employee_number': instance.employeeNumber,
      'first_name': instance.firstName,
      'last_name': instance.lastName,
      'full_name': instance.fullName,
      'email': instance.email,
      'phone_number': instance.phoneNumber,
      'role': instance.role,
      'department': instance.department,
      'title': instance.title,
      'specialization': instance.specialization,
      'registration_number': instance.registrationNumber,
      'joining_date': instance.joiningDate?.toIso8601String(),
      'is_active': instance.isActive,
      'created_at': instance.createdAt.toIso8601String(),
      'updated_at': instance.updatedAt.toIso8601String(),
    };
