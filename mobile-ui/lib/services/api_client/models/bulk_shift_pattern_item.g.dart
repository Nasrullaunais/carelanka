// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'bulk_shift_pattern_item.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

BulkShiftPatternItem _$BulkShiftPatternItemFromJson(
  Map<String, dynamic> json,
) => BulkShiftPatternItem(
  startTime: json['start_time'] as String,
  endTime: json['end_time'] as String,
  requiredRole: StaffRole.fromJson(json['required_role'] as String),
  headcountNeeded: (json['headcount_needed'] as num).toInt(),
  requiredSkillId: json['required_skill_id'] as String?,
  minimumHeadcount: (json['minimum_headcount'] as num?)?.toInt(),
);

Map<String, dynamic> _$BulkShiftPatternItemToJson(
  BulkShiftPatternItem instance,
) => <String, dynamic>{
  'start_time': instance.startTime,
  'end_time': instance.endTime,
  'required_role': instance.requiredRole,
  'required_skill_id': instance.requiredSkillId,
  'headcount_needed': instance.headcountNeeded,
  'minimum_headcount': instance.minimumHeadcount,
};
