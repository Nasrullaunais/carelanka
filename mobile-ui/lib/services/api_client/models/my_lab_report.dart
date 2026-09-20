// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'my_lab_report.g.dart';

@JsonSerializable()
class MyLabReport {
  const MyLabReport({
    required this.id,
    required this.testName,
    required this.fileName,
    required this.contentType,
    required this.byteSize,
    required this.createdAt,
    this.summary,
  });
  
  factory MyLabReport.fromJson(Map<String, Object?> json) => _$MyLabReportFromJson(json);
  
  final String id;
  @JsonKey(name: 'test_name')
  final String testName;
  final String? summary;
  @JsonKey(name: 'file_name')
  final String fileName;
  @JsonKey(name: 'content_type')
  final String contentType;
  @JsonKey(name: 'byte_size')
  final int byteSize;
  @JsonKey(name: 'created_at')
  final DateTime createdAt;

  Map<String, Object?> toJson() => _$MyLabReportToJson(this);
}
