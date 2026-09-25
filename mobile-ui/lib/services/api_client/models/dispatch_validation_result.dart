// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'dispatch_validation_result.g.dart';

@JsonSerializable()
class DispatchValidationResult {
  const DispatchValidationResult({
    this.check,
    this.passed,
    this.detail,
    this.checkedAt,
  });
  
  factory DispatchValidationResult.fromJson(Map<String, Object?> json) => _$DispatchValidationResultFromJson(json);
  
  final String? check;
  final bool? passed;
  final String? detail;
  @JsonKey(name: 'checked_at')
  final DateTime? checkedAt;

  Map<String, Object?> toJson() => _$DispatchValidationResultToJson(this);
}
