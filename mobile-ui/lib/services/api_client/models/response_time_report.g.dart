// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'response_time_report.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

ResponseTimeReport _$ResponseTimeReportFromJson(
  Map<String, dynamic> json,
) => ResponseTimeReport(
  from: json['from'] == null ? null : DateTime.parse(json['from'] as String),
  to: json['to'] == null ? null : DateTime.parse(json['to'] as String),
  rows: (json['rows'] as List<dynamic>?)
      ?.map((e) => ResponseTimeReportRow.fromJson(e as Map<String, dynamic>))
      .toList(),
  totals: json['totals'] == null
      ? null
      : ResponseTimeReportTotals.fromJson(
          json['totals'] as Map<String, dynamic>,
        ),
);

Map<String, dynamic> _$ResponseTimeReportToJson(ResponseTimeReport instance) =>
    <String, dynamic>{
      'from': instance.from?.toIso8601String(),
      'to': instance.to?.toIso8601String(),
      'rows': instance.rows,
      'totals': instance.totals,
    };
