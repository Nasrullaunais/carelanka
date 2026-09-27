// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'fleet_utilisation_report_row.dart';

part 'fleet_utilisation_report.g.dart';

@JsonSerializable()
class FleetUtilisationReport {
  const FleetUtilisationReport({
    this.from,
    this.to,
    this.rows,
  });
  
  factory FleetUtilisationReport.fromJson(Map<String, Object?> json) => _$FleetUtilisationReportFromJson(json);
  
  final DateTime? from;
  final DateTime? to;
  final List<FleetUtilisationReportRow>? rows;

  Map<String, Object?> toJson() => _$FleetUtilisationReportToJson(this);
}
