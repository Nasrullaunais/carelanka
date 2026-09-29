// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'leave_report.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

LeaveReport _$LeaveReportFromJson(Map<String, dynamic> json) => LeaveReport(
  from: json['from'] == null ? null : DateTime.parse(json['from'] as String),
  to: json['to'] == null ? null : DateTime.parse(json['to'] as String),
  groupBy: json['group_by'] as String?,
  rows: (json['rows'] as List<dynamic>?)
      ?.map((e) => LeaveReportRow.fromJson(e as Map<String, dynamic>))
      .toList(),
);

Map<String, dynamic> _$LeaveReportToJson(LeaveReport instance) =>
    <String, dynamic>{
      'from': instance.from?.toIso8601String(),
      'to': instance.to?.toIso8601String(),
      'group_by': instance.groupBy,
      'rows': instance.rows,
    };
