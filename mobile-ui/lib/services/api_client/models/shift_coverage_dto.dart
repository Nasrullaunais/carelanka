// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'coverage_status.dart';

part 'shift_coverage_dto.g.dart';

@JsonSerializable()
class ShiftCoverageDto {
  const ShiftCoverageDto({
    required this.confirmedCount,
    required this.headcountNeeded,
    required this.minimumHeadcount,
    required this.status,
    required this.shortfallToMinimum,
  });
  
  factory ShiftCoverageDto.fromJson(Map<String, Object?> json) => _$ShiftCoverageDtoFromJson(json);
  
  @JsonKey(name: 'confirmed_count')
  final int confirmedCount;
  @JsonKey(name: 'headcount_needed')
  final int headcountNeeded;
  @JsonKey(name: 'minimum_headcount')
  final int minimumHeadcount;
  final CoverageStatus status;
  @JsonKey(name: 'shortfall_to_minimum')
  final int shortfallToMinimum;

  Map<String, Object?> toJson() => _$ShiftCoverageDtoToJson(this);
}
