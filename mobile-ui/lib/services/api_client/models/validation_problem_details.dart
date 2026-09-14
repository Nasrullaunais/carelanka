// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'validation_problem_details.g.dart';

@JsonSerializable()
class ValidationProblemDetails {
  const ValidationProblemDetails({
    this.type,
    this.title,
    this.status,
    this.detail,
    this.instance,
    this.errors,
  });
  
  factory ValidationProblemDetails.fromJson(Map<String, Object?> json) => _$ValidationProblemDetailsFromJson(json);
  
  final String? type;
  final String? title;
  final int? status;
  final String? detail;
  final String? instance;
  final Map<String, List<String>>? errors;

  Map<String, Object?> toJson() => _$ValidationProblemDetailsToJson(this);
}
