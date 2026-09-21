// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'care_workflow_validation.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

CareWorkflowValidation _$CareWorkflowValidationFromJson(
  Map<String, dynamic> json,
) => CareWorkflowValidation(
  passed: json['passed'] as bool?,
  failedRules: (json['failed_rules'] as List<dynamic>?)
      ?.map((e) => e as String)
      .toList(),
);

Map<String, dynamic> _$CareWorkflowValidationToJson(
  CareWorkflowValidation instance,
) => <String, dynamic>{
  'passed': instance.passed,
  'failed_rules': instance.failedRules,
};
