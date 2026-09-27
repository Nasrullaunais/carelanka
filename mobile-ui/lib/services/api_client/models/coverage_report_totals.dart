// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'coverage_report_totals.g.dart';

@JsonSerializable()
class CoverageReportTotals {
  const CoverageReportTotals({
    this.shiftsTotal,
    this.shiftsUnderstaffed,
    this.hoursBelowMinimum,
    this.fillRate,
  });
  
  factory CoverageReportTotals.fromJson(Map<String, Object?> json) => _$CoverageReportTotalsFromJson(json);
  
  @JsonKey(name: 'shifts_total')
  final int? shiftsTotal;
  @JsonKey(name: 'shifts_understaffed')
  final int? shiftsUnderstaffed;
  @JsonKey(name: 'hours_below_minimum')
  final double? hoursBelowMinimum;
  @JsonKey(name: 'fill_rate')
  final double? fillRate;

  Map<String, Object?> toJson() => _$CoverageReportTotalsToJson(this);
}
