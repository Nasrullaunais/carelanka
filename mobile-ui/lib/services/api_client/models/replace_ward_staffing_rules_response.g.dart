// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'replace_ward_staffing_rules_response.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

ReplaceWardStaffingRulesResponse _$ReplaceWardStaffingRulesResponseFromJson(
  Map<String, dynamic> json,
) => ReplaceWardStaffingRulesResponse(
  rules: (json['rules'] as List<dynamic>)
      .map((e) => WardStaffingRuleDto.fromJson(e as Map<String, dynamic>))
      .toList(),
  shiftsNowDisagreeing: (json['shifts_now_disagreeing'] as List<dynamic>)
      .map((e) => ShiftSummaryDto.fromJson(e as Map<String, dynamic>))
      .toList(),
);

Map<String, dynamic> _$ReplaceWardStaffingRulesResponseToJson(
  ReplaceWardStaffingRulesResponse instance,
) => <String, dynamic>{
  'rules': instance.rules,
  'shifts_now_disagreeing': instance.shiftsNowDisagreeing,
};
