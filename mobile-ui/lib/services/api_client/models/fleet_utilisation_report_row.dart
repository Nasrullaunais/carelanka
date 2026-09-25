// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'fleet_utilisation_report_row.g.dart';

@JsonSerializable()
class FleetUtilisationReportRow {
  const FleetUtilisationReportRow({
    this.ambulanceId,
    this.registrationNumber,
    this.runCount,
    this.hoursCommitted,
    this.idleShare,
    this.outOfServiceHours,
  });
  
  factory FleetUtilisationReportRow.fromJson(Map<String, Object?> json) => _$FleetUtilisationReportRowFromJson(json);
  
  @JsonKey(name: 'ambulance_id')
  final String? ambulanceId;
  @JsonKey(name: 'registration_number')
  final String? registrationNumber;
  @JsonKey(name: 'run_count')
  final int? runCount;
  @JsonKey(name: 'hours_committed')
  final double? hoursCommitted;
  @JsonKey(name: 'idle_share')
  final double? idleShare;
  @JsonKey(name: 'out_of_service_hours')
  final double? outOfServiceHours;

  Map<String, Object?> toJson() => _$FleetUtilisationReportRowToJson(this);
}
