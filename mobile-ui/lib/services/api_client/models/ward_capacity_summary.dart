// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'ward_capacity.dart';

part 'ward_capacity_summary.g.dart';

@JsonSerializable()
class WardCapacitySummary {
  const WardCapacitySummary({
    required this.generatedAt,
    required this.wards,
  });
  
  factory WardCapacitySummary.fromJson(Map<String, Object?> json) => _$WardCapacitySummaryFromJson(json);
  
  @JsonKey(name: 'generated_at')
  final DateTime generatedAt;
  final List<WardCapacity> wards;

  Map<String, Object?> toJson() => _$WardCapacitySummaryToJson(this);
}
