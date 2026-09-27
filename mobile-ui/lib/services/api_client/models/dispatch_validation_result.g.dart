// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'dispatch_validation_result.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

DispatchValidationResult _$DispatchValidationResultFromJson(
  Map<String, dynamic> json,
) => DispatchValidationResult(
  check: json['check'] as String?,
  passed: json['passed'] as bool?,
  detail: json['detail'] as String?,
  checkedAt: json['checked_at'] == null
      ? null
      : DateTime.parse(json['checked_at'] as String),
);

Map<String, dynamic> _$DispatchValidationResultToJson(
  DispatchValidationResult instance,
) => <String, dynamic>{
  'check': instance.check,
  'passed': instance.passed,
  'detail': instance.detail,
  'checked_at': instance.checkedAt?.toIso8601String(),
};
