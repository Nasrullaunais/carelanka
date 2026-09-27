// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'coverage_report_row.dart';
import 'coverage_report_totals.dart';

part 'coverage_report.g.dart';

@JsonSerializable()
class CoverageReport {
  const CoverageReport({
    this.from,
    this.to,
    this.rows,
    this.totals,
  });
  
  factory CoverageReport.fromJson(Map<String, Object?> json) => _$CoverageReportFromJson(json);
  
  final DateTime? from;
  final DateTime? to;
  final List<CoverageReportRow>? rows;
  final CoverageReportTotals? totals;

  Map<String, Object?> toJson() => _$CoverageReportToJson(this);
}
