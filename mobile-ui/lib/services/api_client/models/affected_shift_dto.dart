// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'shift_coverage_dto.dart';
import 'shift_summary_dto.dart';

part 'affected_shift_dto.g.dart';

@JsonSerializable()
class AffectedShiftDto {
  const AffectedShiftDto({
    required this.shift,
    required this.coverageIfApproved,
  });
  
  factory AffectedShiftDto.fromJson(Map<String, Object?> json) => _$AffectedShiftDtoFromJson(json);
  
  final ShiftSummaryDto shift;
  @JsonKey(name: 'coverage_if_approved')
  final ShiftCoverageDto coverageIfApproved;

  Map<String, Object?> toJson() => _$AffectedShiftDtoToJson(this);
}
