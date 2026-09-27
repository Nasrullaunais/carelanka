// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'ward_coverage_dto.dart';

part 'ward_coverage_overview_response.g.dart';

@JsonSerializable()
class WardCoverageOverviewResponse {
  const WardCoverageOverviewResponse({
    required this.generatedAt,
    required this.wards,
  });
  
  factory WardCoverageOverviewResponse.fromJson(Map<String, Object?> json) => _$WardCoverageOverviewResponseFromJson(json);
  
  @JsonKey(name: 'generated_at')
  final DateTime generatedAt;
  final List<WardCoverageDto> wards;

  Map<String, Object?> toJson() => _$WardCoverageOverviewResponseToJson(this);
}
