// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'grant_staff_skill_request.g.dart';

@JsonSerializable()
class GrantStaffSkillRequest {
  const GrantStaffSkillRequest({
    required this.skillId,
    this.validFrom,
    this.expiresAt,
  });
  
  factory GrantStaffSkillRequest.fromJson(Map<String, Object?> json) => _$GrantStaffSkillRequestFromJson(json);
  
  @JsonKey(name: 'skill_id')
  final String skillId;
  @JsonKey(name: 'valid_from')
  final DateTime? validFrom;
  @JsonKey(name: 'expires_at')
  final DateTime? expiresAt;

  Map<String, Object?> toJson() => _$GrantStaffSkillRequestToJson(this);
}
