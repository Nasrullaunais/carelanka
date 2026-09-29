// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'affected_shift_dto.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

AffectedShiftDto _$AffectedShiftDtoFromJson(Map<String, dynamic> json) =>
    AffectedShiftDto(
      shift: ShiftSummaryDto.fromJson(json['shift'] as Map<String, dynamic>),
      coverageIfApproved: ShiftCoverageDto.fromJson(
        json['coverage_if_approved'] as Map<String, dynamic>,
      ),
    );

Map<String, dynamic> _$AffectedShiftDtoToJson(AffectedShiftDto instance) =>
    <String, dynamic>{
      'shift': instance.shift,
      'coverage_if_approved': instance.coverageIfApproved,
    };
