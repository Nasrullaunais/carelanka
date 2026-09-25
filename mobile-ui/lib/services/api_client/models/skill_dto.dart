// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'skill_dto.g.dart';

@JsonSerializable()
class SkillDto {
  const SkillDto({
    required this.id,
    required this.name,
    required this.staffCount,
    this.description,
  });
  
  factory SkillDto.fromJson(Map<String, Object?> json) => _$SkillDtoFromJson(json);
  
  final String id;
  final String name;
  final String? description;
  @JsonKey(name: 'staff_count')
  final int staffCount;

  Map<String, Object?> toJson() => _$SkillDtoToJson(this);
}
