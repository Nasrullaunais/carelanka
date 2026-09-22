// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'warning_sweep_result.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

WarningSweepResult _$WarningSweepResultFromJson(Map<String, dynamic> json) =>
    WarningSweepResult(
      raised: (json['raised'] as num).toInt(),
      updated: (json['updated'] as num).toInt(),
      resolved: (json['resolved'] as num).toInt(),
      stillOpen: (json['still_open'] as num).toInt(),
      ranAt: DateTime.parse(json['ran_at'] as String),
    );

Map<String, dynamic> _$WarningSweepResultToJson(WarningSweepResult instance) =>
    <String, dynamic>{
      'raised': instance.raised,
      'updated': instance.updated,
      'resolved': instance.resolved,
      'still_open': instance.stillOpen,
      'ran_at': instance.ranAt.toIso8601String(),
    };
