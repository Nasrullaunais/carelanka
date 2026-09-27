// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'call_priority.dart';

part 'response_time_report_row.g.dart';

@JsonSerializable()
class ResponseTimeReportRow {
  const ResponseTimeReportRow({
    this.priority,
    this.callCount,
    this.medianMinutesToDispatch,
    this.medianMinutesToArrival,
    this.slowestMinutesToArrival,
  });
  
  factory ResponseTimeReportRow.fromJson(Map<String, Object?> json) => _$ResponseTimeReportRowFromJson(json);
  
  final CallPriority? priority;
  @JsonKey(name: 'call_count')
  final int? callCount;
  @JsonKey(name: 'median_minutes_to_dispatch')
  final double? medianMinutesToDispatch;
  @JsonKey(name: 'median_minutes_to_arrival')
  final double? medianMinutesToArrival;
  @JsonKey(name: 'slowest_minutes_to_arrival')
  final double? slowestMinutesToArrival;

  Map<String, Object?> toJson() => _$ResponseTimeReportRowToJson(this);
}
