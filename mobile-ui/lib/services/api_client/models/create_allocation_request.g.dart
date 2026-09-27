// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'create_allocation_request.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

CreateAllocationRequest _$CreateAllocationRequestFromJson(
  Map<String, dynamic> json,
) => CreateAllocationRequest(
  shiftId: json['shift_id'] as String,
  staffMemberId: json['staff_member_id'] as String,
  override: json['override'] as bool?,
  overrideReason: json['override_reason'] as String?,
);

Map<String, dynamic> _$CreateAllocationRequestToJson(
  CreateAllocationRequest instance,
) => <String, dynamic>{
  'shift_id': instance.shiftId,
  'staff_member_id': instance.staffMemberId,
  'override': instance.override,
  'override_reason': instance.overrideReason,
};
