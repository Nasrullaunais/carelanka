// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'leave_report_row.g.dart';

@JsonSerializable()
class LeaveReportRow {
  const LeaveReportRow({
    this.key,
    this.approvedDays,
    this.pendingDays,
    this.rejectedCount,
    this.sickDays,
  });
  
  factory LeaveReportRow.fromJson(Map<String, Object?> json) => _$LeaveReportRowFromJson(json);
  
  final String? key;
  @JsonKey(name: 'approved_days')
  final double? approvedDays;
  @JsonKey(name: 'pending_days')
  final double? pendingDays;
  @JsonKey(name: 'rejected_count')
  final int? rejectedCount;
  @JsonKey(name: 'sick_days')
  final double? sickDays;

  Map<String, Object?> toJson() => _$LeaveReportRowToJson(this);
}
