// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'suggested_bed.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

SuggestedBed _$SuggestedBedFromJson(Map<String, dynamic> json) => SuggestedBed(
  bedId: json['bed_id'] as String,
  wardName: json['ward_name'] as String?,
  bedNumber: json['bed_number'] as String?,
  isDowngrade: json['is_downgrade'] as bool,
  requiresDutyManager: json['requires_duty_manager'] as bool,
  rulesSatisfied: (json['rules_satisfied'] as List<dynamic>?)
      ?.map((e) => e as String)
      .toList(),
  rationale: json['rationale'] as String?,
);

Map<String, dynamic> _$SuggestedBedToJson(SuggestedBed instance) =>
    <String, dynamic>{
      'bed_id': instance.bedId,
      'ward_name': instance.wardName,
      'bed_number': instance.bedNumber,
      'is_downgrade': instance.isDowngrade,
      'requires_duty_manager': instance.requiresDutyManager,
      'rules_satisfied': instance.rulesSatisfied,
      'rationale': instance.rationale,
    };
