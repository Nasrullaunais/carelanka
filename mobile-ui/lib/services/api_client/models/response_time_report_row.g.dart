// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'response_time_report_row.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

ResponseTimeReportRow _$ResponseTimeReportRowFromJson(
  Map<String, dynamic> json,
) => ResponseTimeReportRow(
  priority: json['priority'] == null
      ? null
      : CallPriority.fromJson(json['priority'] as String),
  callCount: (json['call_count'] as num?)?.toInt(),
  medianMinutesToDispatch: (json['median_minutes_to_dispatch'] as num?)
      ?.toDouble(),
  medianMinutesToArrival: (json['median_minutes_to_arrival'] as num?)
      ?.toDouble(),
  slowestMinutesToArrival: (json['slowest_minutes_to_arrival'] as num?)
      ?.toDouble(),
);

Map<String, dynamic> _$ResponseTimeReportRowToJson(
  ResponseTimeReportRow instance,
) => <String, dynamic>{
  'priority': instance.priority,
  'call_count': instance.callCount,
  'median_minutes_to_dispatch': instance.medianMinutesToDispatch,
  'median_minutes_to_arrival': instance.medianMinutesToArrival,
  'slowest_minutes_to_arrival': instance.slowestMinutesToArrival,
};
