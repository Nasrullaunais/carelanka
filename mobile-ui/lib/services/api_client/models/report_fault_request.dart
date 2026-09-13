// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'report_fault_request.g.dart';

@JsonSerializable()
class ReportFaultRequest {
  const ReportFaultRequest({
    required this.description,
  });
  
  factory ReportFaultRequest.fromJson(Map<String, Object?> json) => _$ReportFaultRequestFromJson(json);
  
  final String description;

  Map<String, Object?> toJson() => _$ReportFaultRequestToJson(this);
}
