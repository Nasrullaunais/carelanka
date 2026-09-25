// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'response_time_report_totals.g.dart';

@JsonSerializable()
class ResponseTimeReportTotals {
  const ResponseTimeReportTotals({
    this.callCount,
    this.medianMinutesToDispatch,
    this.medianMinutesToArrival,
  });

  factory ResponseTimeReportTotals.fromJson(Map<String, Object?> json) =>
      _$ResponseTimeReportTotalsFromJson(json);

  @JsonKey(name: 'call_count')
  final int? callCount;
  @JsonKey(name: 'median_minutes_to_dispatch')
  final double? medianMinutesToDispatch;
  @JsonKey(name: 'median_minutes_to_arrival')
  final double? medianMinutesToArrival;

  Map<String, Object?> toJson() => _$ResponseTimeReportTotalsToJson(this);
}
