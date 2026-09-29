// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'roster_validation_result.g.dart';

@JsonSerializable()
class RosterValidationResult {
  const RosterValidationResult({
    required this.check,
    required this.passed,
    required this.detail,
    required this.checkedAt,
  });
  
  factory RosterValidationResult.fromJson(Map<String, Object?> json) => _$RosterValidationResultFromJson(json);
  
  final String check;
  final bool passed;
  final String detail;
  @JsonKey(name: 'checked_at')
  final DateTime checkedAt;

  Map<String, Object?> toJson() => _$RosterValidationResultToJson(this);
}
