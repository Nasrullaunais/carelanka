// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'gender_policy.dart';
import 'ward_type.dart';

part 'ward.g.dart';

@JsonSerializable()
class Ward {
  const Ward({
    required this.id,
    required this.name,
    required this.wardType,
    required this.genderPolicy,
    required this.isActive,
    required this.totalBeds,
    this.createdAt,
    this.updatedAt,
  });
  
  factory Ward.fromJson(Map<String, Object?> json) => _$WardFromJson(json);
  
  final String id;
  final String name;
  @JsonKey(name: 'ward_type')
  final WardType wardType;
  @JsonKey(name: 'gender_policy')
  final GenderPolicy genderPolicy;
  @JsonKey(name: 'is_active')
  final bool isActive;
  @JsonKey(name: 'total_beds')
  final int totalBeds;
  @JsonKey(name: 'created_at')
  final DateTime? createdAt;
  @JsonKey(name: 'updated_at')
  final DateTime? updatedAt;

  Map<String, Object?> toJson() => _$WardToJson(this);
}
