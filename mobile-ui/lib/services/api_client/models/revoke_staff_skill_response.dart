// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'allocation_summary_dto.dart';

part 'revoke_staff_skill_response.g.dart';

@JsonSerializable()
class RevokeStaffSkillResponse {
  const RevokeStaffSkillResponse({
    required this.affectedAllocations,
  });
  
  factory RevokeStaffSkillResponse.fromJson(Map<String, Object?> json) => _$RevokeStaffSkillResponseFromJson(json);
  
  @JsonKey(name: 'affected_allocations')
  final List<AllocationSummaryDto> affectedAllocations;

  Map<String, Object?> toJson() => _$RevokeStaffSkillResponseToJson(this);
}
