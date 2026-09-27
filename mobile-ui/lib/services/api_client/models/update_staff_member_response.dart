// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'allocation_summary_dto.dart';
import 'staff_member_dto.dart';

part 'update_staff_member_response.g.dart';

@JsonSerializable()
class UpdateStaffMemberResponse {
  const UpdateStaffMemberResponse({
    required this.staffMember,
    required this.affectedAllocations,
  });
  
  factory UpdateStaffMemberResponse.fromJson(Map<String, Object?> json) => _$UpdateStaffMemberResponseFromJson(json);
  
  @JsonKey(name: 'staff_member')
  final StaffMemberDto staffMember;
  @JsonKey(name: 'affected_allocations')
  final List<AllocationSummaryDto> affectedAllocations;

  Map<String, Object?> toJson() => _$UpdateStaffMemberResponseToJson(this);
}
