// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'update_skill_request.g.dart';

@JsonSerializable()
class UpdateSkillRequest {
  const UpdateSkillRequest({
    required this.name,
    this.description,
  });
  
  factory UpdateSkillRequest.fromJson(Map<String, Object?> json) => _$UpdateSkillRequestFromJson(json);
  
  final String name;
  final String? description;

  Map<String, Object?> toJson() => _$UpdateSkillRequestToJson(this);
}
