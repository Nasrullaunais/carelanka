// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'ward_coverage_dto.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

WardCoverageDto _$WardCoverageDtoFromJson(Map<String, dynamic> json) =>
    WardCoverageDto(
      wardId: json['ward_id'] as String,
      wardName: json['ward_name'] as String,
      onDutyCount: (json['on_duty_count'] as num).toInt(),
      minimumHeadcount: (json['minimum_headcount'] as num).toInt(),
      headcountNeeded: (json['headcount_needed'] as num).toInt(),
      status: CoverageStatus.fromJson(json['status'] as String),
      byRole: Map<String, int>.from(json['by_role'] as Map),
      currentShiftId: json['current_shift_id'] as String?,
    );

Map<String, dynamic> _$WardCoverageDtoToJson(WardCoverageDto instance) =>
    <String, dynamic>{
      'ward_id': instance.wardId,
      'ward_name': instance.wardName,
      'current_shift_id': instance.currentShiftId,
      'on_duty_count': instance.onDutyCount,
      'minimum_headcount': instance.minimumHeadcount,
      'headcount_needed': instance.headcountNeeded,
      'status': instance.status,
      'by_role': instance.byRole,
    };
