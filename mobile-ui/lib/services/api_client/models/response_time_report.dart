// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'response_time_report_row.dart';
import 'response_time_report_totals.dart';

part 'response_time_report.g.dart';

@JsonSerializable()
class ResponseTimeReport {
  const ResponseTimeReport({this.from, this.to, this.rows, this.totals});

  factory ResponseTimeReport.fromJson(Map<String, Object?> json) =>
      _$ResponseTimeReportFromJson(json);

  final DateTime? from;
  final DateTime? to;
  final List<ResponseTimeReportRow>? rows;
  final ResponseTimeReportTotals? totals;

  Map<String, Object?> toJson() => _$ResponseTimeReportToJson(this);
}
