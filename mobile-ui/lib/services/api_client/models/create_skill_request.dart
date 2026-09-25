// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'create_skill_request.g.dart';

@JsonSerializable()
class CreateSkillRequest {
  const CreateSkillRequest({
    required this.name,
    this.description,
  });
  
  factory CreateSkillRequest.fromJson(Map<String, Object?> json) => _$CreateSkillRequestFromJson(json);
  
  final String name;
  final String? description;

  Map<String, Object?> toJson() => _$CreateSkillRequestToJson(this);
}
