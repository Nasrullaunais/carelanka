// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'leave_report_row.dart';

part 'leave_report.g.dart';

@JsonSerializable()
class LeaveReport {
  const LeaveReport({
    this.from,
    this.to,
    this.groupBy,
    this.rows,
  });
  
  factory LeaveReport.fromJson(Map<String, Object?> json) => _$LeaveReportFromJson(json);
  
  final DateTime? from;
  final DateTime? to;
  @JsonKey(name: 'group_by')
  final String? groupBy;
  final List<LeaveReportRow>? rows;

  Map<String, Object?> toJson() => _$LeaveReportToJson(this);
}
