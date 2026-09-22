// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'my_lab_report.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

MyLabReport _$MyLabReportFromJson(Map<String, dynamic> json) => MyLabReport(
  id: json['id'] as String,
  testName: json['test_name'] as String,
  fileName: json['file_name'] as String,
  contentType: json['content_type'] as String,
  byteSize: (json['byte_size'] as num).toInt(),
  createdAt: DateTime.parse(json['created_at'] as String),
  summary: json['summary'] as String?,
);

Map<String, dynamic> _$MyLabReportToJson(MyLabReport instance) =>
    <String, dynamic>{
      'id': instance.id,
      'test_name': instance.testName,
      'summary': instance.summary,
      'file_name': instance.fileName,
      'content_type': instance.contentType,
      'byte_size': instance.byteSize,
      'created_at': instance.createdAt.toIso8601String(),
    };
