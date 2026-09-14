// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'health_status.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

HealthStatus _$HealthStatusFromJson(Map<String, dynamic> json) => HealthStatus(
  status: json['status'] as String?,
  database: json['database'] as String?,
  version: json['version'] as String?,
  checkedAt: json['checked_at'] == null
      ? null
      : DateTime.parse(json['checked_at'] as String),
);

Map<String, dynamic> _$HealthStatusToJson(HealthStatus instance) =>
    <String, dynamic>{
      'status': instance.status,
      'database': instance.database,
      'version': instance.version,
      'checked_at': instance.checkedAt?.toIso8601String(),
    };
