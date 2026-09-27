// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'shift_detail_dto.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

ShiftDetailDto _$ShiftDetailDtoFromJson(Map<String, dynamic> json) =>
    ShiftDetailDto(
      id: json['id'] as String,
      wardId: json['ward_id'] as String,
      wardName: json['ward_name'] as String,
      date: DateTime.parse(json['date'] as String),
      startTime: json['start_time'] as String,
      endTime: json['end_time'] as String,
      crossesMidnight: json['crosses_midnight'] as bool,
      requiredRole: StaffRole.fromJson(json['required_role'] as String),
      headcountNeeded: (json['headcount_needed'] as num).toInt(),
      minimumHeadcount: (json['minimum_headcount'] as num).toInt(),
      createdAt: DateTime.parse(json['created_at'] as String),
      updatedAt: DateTime.parse(json['updated_at'] as String),
      coverage: ShiftCoverageDto.fromJson(
        json['coverage'] as Map<String, dynamic>,
      ),
      allocations: (json['allocations'] as List<dynamic>)
          .map((e) => AllocationDto.fromJson(e as Map<String, dynamic>))
          .toList(),
      requiredSkillId: json['required_skill_id'] as String?,
      requiredSkillName: json['required_skill_name'] as String?,
      openProposalId: json['open_proposal_id'] as String?,
    );

Map<String, dynamic> _$ShiftDetailDtoToJson(ShiftDetailDto instance) =>
    <String, dynamic>{
      'id': instance.id,
      'ward_id': instance.wardId,
      'ward_name': instance.wardName,
      'date': instance.date.toIso8601String(),
      'start_time': instance.startTime,
      'end_time': instance.endTime,
      'crosses_midnight': instance.crossesMidnight,
      'required_role': instance.requiredRole,
      'required_skill_id': instance.requiredSkillId,
      'required_skill_name': instance.requiredSkillName,
      'headcount_needed': instance.headcountNeeded,
      'minimum_headcount': instance.minimumHeadcount,
      'created_at': instance.createdAt.toIso8601String(),
      'updated_at': instance.updatedAt.toIso8601String(),
      'coverage': instance.coverage,
      'allocations': instance.allocations,
      'open_proposal_id': instance.openProposalId,
    };
