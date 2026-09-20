// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'bed_workflow_validation.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

BedWorkflowValidation _$BedWorkflowValidationFromJson(
  Map<String, dynamic> json,
) => BedWorkflowValidation(
  passed: json['passed'] as bool?,
  failedRules: (json['failed_rules'] as List<dynamic>?)
      ?.map((e) => e as String)
      .toList(),
);

Map<String, dynamic> _$BedWorkflowValidationToJson(
  BedWorkflowValidation instance,
) => <String, dynamic>{
  'passed': instance.passed,
  'failed_rules': instance.failedRules,
};
