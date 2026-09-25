// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'fleet_utilisation_report.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

FleetUtilisationReport _$FleetUtilisationReportFromJson(
  Map<String, dynamic> json,
) => FleetUtilisationReport(
  from: json['from'] == null ? null : DateTime.parse(json['from'] as String),
  to: json['to'] == null ? null : DateTime.parse(json['to'] as String),
  rows: (json['rows'] as List<dynamic>?)
      ?.map(
        (e) => FleetUtilisationReportRow.fromJson(e as Map<String, dynamic>),
      )
      .toList(),
);

Map<String, dynamic> _$FleetUtilisationReportToJson(
  FleetUtilisationReport instance,
) => <String, dynamic>{
  'from': instance.from?.toIso8601String(),
  'to': instance.to?.toIso8601String(),
  'rows': instance.rows,
};
