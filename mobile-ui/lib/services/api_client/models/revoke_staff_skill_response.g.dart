// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'revoke_staff_skill_response.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

RevokeStaffSkillResponse _$RevokeStaffSkillResponseFromJson(
  Map<String, dynamic> json,
) => RevokeStaffSkillResponse(
  affectedAllocations: (json['affected_allocations'] as List<dynamic>)
      .map((e) => AllocationSummaryDto.fromJson(e as Map<String, dynamic>))
      .toList(),
);

Map<String, dynamic> _$RevokeStaffSkillResponseToJson(
  RevokeStaffSkillResponse instance,
) => <String, dynamic>{'affected_allocations': instance.affectedAllocations};
