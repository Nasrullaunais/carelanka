// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'gender_policy.dart';
import 'ward_type.dart';

part 'create_ward_request.g.dart';

@JsonSerializable()
class CreateWardRequest {
  const CreateWardRequest({
    required this.name,
    required this.wardType,
    required this.genderPolicy,
    this.isActive = true,
  });
  
  factory CreateWardRequest.fromJson(Map<String, Object?> json) => _$CreateWardRequestFromJson(json);
  
  final String name;
  @JsonKey(name: 'ward_type')
  final WardType wardType;
  @JsonKey(name: 'gender_policy')
  final GenderPolicy genderPolicy;
  @JsonKey(name: 'is_active')
  final bool isActive;

  Map<String, Object?> toJson() => _$CreateWardRequestToJson(this);
}
