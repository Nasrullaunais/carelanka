// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'coverage_status.dart';

part 'ward_coverage_dto.g.dart';

@JsonSerializable()
class WardCoverageDto {
  const WardCoverageDto({
    required this.wardId,
    required this.wardName,
    required this.onDutyCount,
    required this.minimumHeadcount,
    required this.headcountNeeded,
    required this.status,
    required this.byRole,
    this.currentShiftId,
  });
  
  factory WardCoverageDto.fromJson(Map<String, Object?> json) => _$WardCoverageDtoFromJson(json);
  
  @JsonKey(name: 'ward_id')
  final String wardId;
  @JsonKey(name: 'ward_name')
  final String wardName;
  @JsonKey(name: 'current_shift_id')
  final String? currentShiftId;
  @JsonKey(name: 'on_duty_count')
  final int onDutyCount;
  @JsonKey(name: 'minimum_headcount')
  final int minimumHeadcount;
  @JsonKey(name: 'headcount_needed')
  final int headcountNeeded;
  final CoverageStatus status;
  @JsonKey(name: 'by_role')
  final Map<String, int> byRole;

  Map<String, Object?> toJson() => _$WardCoverageDtoToJson(this);
}
