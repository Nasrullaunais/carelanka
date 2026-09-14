// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'gender_policy.dart';
import 'ward_type.dart';

part 'ward_capacity.g.dart';

@JsonSerializable()
class WardCapacity {
  const WardCapacity({
    required this.wardId,
    required this.name,
    required this.wardType,
    required this.genderPolicy,
    required this.totalBeds,
    required this.freeBeds,
  });
  
  factory WardCapacity.fromJson(Map<String, Object?> json) => _$WardCapacityFromJson(json);
  
  @JsonKey(name: 'ward_id')
  final String wardId;
  final String name;
  @JsonKey(name: 'ward_type')
  final WardType wardType;
  @JsonKey(name: 'gender_policy')
  final GenderPolicy genderPolicy;
  @JsonKey(name: 'total_beds')
  final int totalBeds;
  @JsonKey(name: 'free_beds')
  final int freeBeds;

  Map<String, Object?> toJson() => _$WardCapacityToJson(this);
}
