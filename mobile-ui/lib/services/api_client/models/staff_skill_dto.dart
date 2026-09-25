// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'staff_skill_dto.g.dart';

@JsonSerializable()
class StaffSkillDto {
  const StaffSkillDto({
    required this.skillId,
    required this.skillName,
    required this.isValid,
    required this.grantedAt,
    this.validFrom,
    this.expiresAt,
  });
  
  factory StaffSkillDto.fromJson(Map<String, Object?> json) => _$StaffSkillDtoFromJson(json);
  
  @JsonKey(name: 'skill_id')
  final String skillId;
  @JsonKey(name: 'skill_name')
  final String skillName;
  @JsonKey(name: 'valid_from')
  final DateTime? validFrom;
  @JsonKey(name: 'expires_at')
  final DateTime? expiresAt;
  @JsonKey(name: 'is_valid')
  final bool isValid;
  @JsonKey(name: 'granted_at')
  final DateTime grantedAt;

  Map<String, Object?> toJson() => _$StaffSkillDtoToJson(this);
}
