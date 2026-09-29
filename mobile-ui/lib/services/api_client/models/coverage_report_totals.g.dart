// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'coverage_report_totals.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

CoverageReportTotals _$CoverageReportTotalsFromJson(
  Map<String, dynamic> json,
) => CoverageReportTotals(
  shiftsTotal: (json['shifts_total'] as num?)?.toInt(),
  shiftsUnderstaffed: (json['shifts_understaffed'] as num?)?.toInt(),
  hoursBelowMinimum: (json['hours_below_minimum'] as num?)?.toDouble(),
  fillRate: (json['fill_rate'] as num?)?.toDouble(),
);

Map<String, dynamic> _$CoverageReportTotalsToJson(
  CoverageReportTotals instance,
) => <String, dynamic>{
  'shifts_total': instance.shiftsTotal,
  'shifts_understaffed': instance.shiftsUnderstaffed,
  'hours_below_minimum': instance.hoursBelowMinimum,
  'fill_rate': instance.fillRate,
};
