// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'shift_coverage_dto.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

ShiftCoverageDto _$ShiftCoverageDtoFromJson(Map<String, dynamic> json) =>
    ShiftCoverageDto(
      confirmedCount: (json['confirmed_count'] as num).toInt(),
      headcountNeeded: (json['headcount_needed'] as num).toInt(),
      minimumHeadcount: (json['minimum_headcount'] as num).toInt(),
      status: CoverageStatus.fromJson(json['status'] as String),
      shortfallToMinimum: (json['shortfall_to_minimum'] as num).toInt(),
    );

Map<String, dynamic> _$ShiftCoverageDtoToJson(ShiftCoverageDto instance) =>
    <String, dynamic>{
      'confirmed_count': instance.confirmedCount,
      'headcount_needed': instance.headcountNeeded,
      'minimum_headcount': instance.minimumHeadcount,
      'status': instance.status,
      'shortfall_to_minimum': instance.shortfallToMinimum,
    };
