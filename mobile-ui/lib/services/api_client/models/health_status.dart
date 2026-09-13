// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'health_status.g.dart';

@JsonSerializable()
class HealthStatus {
  const HealthStatus({
    this.status,
    this.database,
    this.version,
    this.checkedAt,
  });
  
  factory HealthStatus.fromJson(Map<String, Object?> json) => _$HealthStatusFromJson(json);
  
  final String? status;
  final String? database;
  final String? version;
  @JsonKey(name: 'checked_at')
  final DateTime? checkedAt;

  Map<String, Object?> toJson() => _$HealthStatusToJson(this);
}
