// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'staff_summary_dto.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

StaffSummaryDto _$StaffSummaryDtoFromJson(Map<String, dynamic> json) =>
    StaffSummaryDto(
      id: json['id'] as String,
      fullName: json['full_name'] as String,
      role: StaffRole.fromJson(json['role'] as String),
      isActive: json['is_active'] as bool,
      skillCount: (json['skill_count'] as num).toInt(),
      department: json['department'] as String?,
    );

Map<String, dynamic> _$StaffSummaryDtoToJson(StaffSummaryDto instance) =>
    <String, dynamic>{
      'id': instance.id,
      'full_name': instance.fullName,
      'role': instance.role,
      'department': instance.department,
      'is_active': instance.isActive,
      'skill_count': instance.skillCount,
    };
