// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'leave_report_row.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

LeaveReportRow _$LeaveReportRowFromJson(Map<String, dynamic> json) =>
    LeaveReportRow(
      key: json['key'] as String?,
      approvedDays: (json['approved_days'] as num?)?.toDouble(),
      pendingDays: (json['pending_days'] as num?)?.toDouble(),
      rejectedCount: (json['rejected_count'] as num?)?.toInt(),
      sickDays: (json['sick_days'] as num?)?.toDouble(),
    );

Map<String, dynamic> _$LeaveReportRowToJson(LeaveReportRow instance) =>
    <String, dynamic>{
      'key': instance.key,
      'approved_days': instance.approvedDays,
      'pending_days': instance.pendingDays,
      'rejected_count': instance.rejectedCount,
      'sick_days': instance.sickDays,
    };
