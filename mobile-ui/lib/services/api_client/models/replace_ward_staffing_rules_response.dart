// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'shift_summary_dto.dart';
import 'ward_staffing_rule_dto.dart';

part 'replace_ward_staffing_rules_response.g.dart';

@JsonSerializable()
class ReplaceWardStaffingRulesResponse {
  const ReplaceWardStaffingRulesResponse({
    required this.rules,
    required this.shiftsNowDisagreeing,
  });
  
  factory ReplaceWardStaffingRulesResponse.fromJson(Map<String, Object?> json) => _$ReplaceWardStaffingRulesResponseFromJson(json);
  
  final List<WardStaffingRuleDto> rules;
  @JsonKey(name: 'shifts_now_disagreeing')
  final List<ShiftSummaryDto> shiftsNowDisagreeing;

  Map<String, Object?> toJson() => _$ReplaceWardStaffingRulesResponseToJson(this);
}
