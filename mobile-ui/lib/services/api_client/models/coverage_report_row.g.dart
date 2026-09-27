// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'coverage_report_row.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

CoverageReportRow _$CoverageReportRowFromJson(Map<String, dynamic> json) =>
    CoverageReportRow(
      wardId: json['ward_id'] as String?,
      wardName: json['ward_name'] as String?,
      date: json['date'] == null
          ? null
          : DateTime.parse(json['date'] as String),
      shiftsTotal: (json['shifts_total'] as num?)?.toInt(),
      shiftsUnderstaffed: (json['shifts_understaffed'] as num?)?.toInt(),
      hoursBelowMinimum: (json['hours_below_minimum'] as num?)?.toDouble(),
      fillRate: (json['fill_rate'] as num?)?.toDouble(),
    );

Map<String, dynamic> _$CoverageReportRowToJson(CoverageReportRow instance) =>
    <String, dynamic>{
      'ward_id': instance.wardId,
      'ward_name': instance.wardName,
      'date': instance.date?.toIso8601String(),
      'shifts_total': instance.shiftsTotal,
      'shifts_understaffed': instance.shiftsUnderstaffed,
      'hours_below_minimum': instance.hoursBelowMinimum,
      'fill_rate': instance.fillRate,
    };
