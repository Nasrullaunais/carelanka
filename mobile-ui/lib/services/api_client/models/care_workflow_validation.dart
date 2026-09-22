// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'care_workflow_validation.g.dart';

@JsonSerializable()
class CareWorkflowValidation {
  const CareWorkflowValidation({
    this.passed,
    this.failedRules,
  });
  
  factory CareWorkflowValidation.fromJson(Map<String, Object?> json) => _$CareWorkflowValidationFromJson(json);
  
  final bool? passed;
  @JsonKey(name: 'failed_rules')
  final List<String>? failedRules;

  Map<String, Object?> toJson() => _$CareWorkflowValidationToJson(this);
}
