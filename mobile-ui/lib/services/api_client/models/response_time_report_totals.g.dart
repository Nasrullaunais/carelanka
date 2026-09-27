// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'response_time_report_totals.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

ResponseTimeReportTotals _$ResponseTimeReportTotalsFromJson(
  Map<String, dynamic> json,
) => ResponseTimeReportTotals(
  callCount: (json['call_count'] as num?)?.toInt(),
  medianMinutesToDispatch: (json['median_minutes_to_dispatch'] as num?)
      ?.toDouble(),
  medianMinutesToArrival: (json['median_minutes_to_arrival'] as num?)
      ?.toDouble(),
);

Map<String, dynamic> _$ResponseTimeReportTotalsToJson(
  ResponseTimeReportTotals instance,
) => <String, dynamic>{
  'call_count': instance.callCount,
  'median_minutes_to_dispatch': instance.medianMinutesToDispatch,
  'median_minutes_to_arrival': instance.medianMinutesToArrival,
};
