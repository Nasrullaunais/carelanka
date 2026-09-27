// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'create_shift_request.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

CreateShiftRequest _$CreateShiftRequestFromJson(Map<String, dynamic> json) =>
    CreateShiftRequest(
      wardId: json['ward_id'] as String,
      date: DateTime.parse(json['date'] as String),
      startTime: json['start_time'] as String,
      endTime: json['end_time'] as String,
      requiredRole: StaffRole.fromJson(json['required_role'] as String),
      headcountNeeded: (json['headcount_needed'] as num).toInt(),
      requiredSkillId: json['required_skill_id'] as String?,
      minimumHeadcount: (json['minimum_headcount'] as num?)?.toInt(),
    );

Map<String, dynamic> _$CreateShiftRequestToJson(CreateShiftRequest instance) =>
    <String, dynamic>{
      'ward_id': instance.wardId,
      'date': instance.date.toIso8601String(),
      'start_time': instance.startTime,
      'end_time': instance.endTime,
      'required_role': instance.requiredRole,
      'required_skill_id': instance.requiredSkillId,
      'headcount_needed': instance.headcountNeeded,
      'minimum_headcount': instance.minimumHeadcount,
    };
