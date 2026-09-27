// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'ward_coverage_overview_response.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

WardCoverageOverviewResponse _$WardCoverageOverviewResponseFromJson(
  Map<String, dynamic> json,
) => WardCoverageOverviewResponse(
  generatedAt: DateTime.parse(json['generated_at'] as String),
  wards: (json['wards'] as List<dynamic>)
      .map((e) => WardCoverageDto.fromJson(e as Map<String, dynamic>))
      .toList(),
);

Map<String, dynamic> _$WardCoverageOverviewResponseToJson(
  WardCoverageOverviewResponse instance,
) => <String, dynamic>{
  'generated_at': instance.generatedAt.toIso8601String(),
  'wards': instance.wards,
};
