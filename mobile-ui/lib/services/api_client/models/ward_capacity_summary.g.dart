// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'ward_capacity_summary.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

WardCapacitySummary _$WardCapacitySummaryFromJson(Map<String, dynamic> json) =>
    WardCapacitySummary(
      generatedAt: DateTime.parse(json['generated_at'] as String),
      wards: (json['wards'] as List<dynamic>)
          .map((e) => WardCapacity.fromJson(e as Map<String, dynamic>))
          .toList(),
    );

Map<String, dynamic> _$WardCapacitySummaryToJson(
  WardCapacitySummary instance,
) => <String, dynamic>{
  'generated_at': instance.generatedAt.toIso8601String(),
  'wards': instance.wards,
};
