// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'deactivate_staff_member_request.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

DeactivateStaffMemberRequest _$DeactivateStaffMemberRequestFromJson(
  Map<String, dynamic> json,
) => DeactivateStaffMemberRequest(
  reason: json['reason'] as String,
  effectiveDate: json['effective_date'] == null
      ? null
      : DateTime.parse(json['effective_date'] as String),
);

Map<String, dynamic> _$DeactivateStaffMemberRequestToJson(
  DeactivateStaffMemberRequest instance,
) => <String, dynamic>{
  'reason': instance.reason,
  'effective_date': instance.effectiveDate?.toIso8601String(),
};
