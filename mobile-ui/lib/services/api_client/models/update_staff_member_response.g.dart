// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'update_staff_member_response.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

UpdateStaffMemberResponse _$UpdateStaffMemberResponseFromJson(
  Map<String, dynamic> json,
) => UpdateStaffMemberResponse(
  staffMember: StaffMemberDto.fromJson(
    json['staff_member'] as Map<String, dynamic>,
  ),
  affectedAllocations: (json['affected_allocations'] as List<dynamic>)
      .map((e) => AllocationSummaryDto.fromJson(e as Map<String, dynamic>))
      .toList(),
);

Map<String, dynamic> _$UpdateStaffMemberResponseToJson(
  UpdateStaffMemberResponse instance,
) => <String, dynamic>{
  'staff_member': instance.staffMember,
  'affected_allocations': instance.affectedAllocations,
};
