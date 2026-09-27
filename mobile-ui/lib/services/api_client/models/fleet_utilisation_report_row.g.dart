// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'fleet_utilisation_report_row.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

FleetUtilisationReportRow _$FleetUtilisationReportRowFromJson(
  Map<String, dynamic> json,
) => FleetUtilisationReportRow(
  ambulanceId: json['ambulance_id'] as String?,
  registrationNumber: json['registration_number'] as String?,
  runCount: (json['run_count'] as num?)?.toInt(),
  hoursCommitted: (json['hours_committed'] as num?)?.toDouble(),
  idleShare: (json['idle_share'] as num?)?.toDouble(),
  outOfServiceHours: (json['out_of_service_hours'] as num?)?.toDouble(),
);

Map<String, dynamic> _$FleetUtilisationReportRowToJson(
  FleetUtilisationReportRow instance,
) => <String, dynamic>{
  'ambulance_id': instance.ambulanceId,
  'registration_number': instance.registrationNumber,
  'run_count': instance.runCount,
  'hours_committed': instance.hoursCommitted,
  'idle_share': instance.idleShare,
  'out_of_service_hours': instance.outOfServiceHours,
};
