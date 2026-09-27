// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'roster_validation_result.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

RosterValidationResult _$RosterValidationResultFromJson(
  Map<String, dynamic> json,
) => RosterValidationResult(
  check: json['check'] as String,
  passed: json['passed'] as bool,
  detail: json['detail'] as String,
  checkedAt: DateTime.parse(json['checked_at'] as String),
);

Map<String, dynamic> _$RosterValidationResultToJson(
  RosterValidationResult instance,
) => <String, dynamic>{
  'check': instance.check,
  'passed': instance.passed,
  'detail': instance.detail,
  'checked_at': instance.checkedAt.toIso8601String(),
};
