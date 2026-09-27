// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'staff_lookup_result.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

StaffLookupResult _$StaffLookupResultFromJson(Map<String, dynamic> json) =>
    StaffLookupResult(
      staffId: json['staff_id'] as String,
      found: json['found'] as bool,
      fullName: json['full_name'] as String?,
      role: json['role'] == null
          ? null
          : StaffRole.fromJson(json['role'] as String),
      isActive: json['is_active'] as bool?,
    );

Map<String, dynamic> _$StaffLookupResultToJson(StaffLookupResult instance) =>
    <String, dynamic>{
      'staff_id': instance.staffId,
      'found': instance.found,
      'full_name': instance.fullName,
      'role': instance.role,
      'is_active': instance.isActive,
    };
