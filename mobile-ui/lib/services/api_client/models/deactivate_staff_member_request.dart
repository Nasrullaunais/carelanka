// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'deactivate_staff_member_request.g.dart';

@JsonSerializable()
class DeactivateStaffMemberRequest {
  const DeactivateStaffMemberRequest({
    required this.reason,
    this.effectiveDate,
  });
  
  factory DeactivateStaffMemberRequest.fromJson(Map<String, Object?> json) => _$DeactivateStaffMemberRequestFromJson(json);
  
  final String reason;
  @JsonKey(name: 'effective_date')
  final DateTime? effectiveDate;

  Map<String, Object?> toJson() => _$DeactivateStaffMemberRequestToJson(this);
}
