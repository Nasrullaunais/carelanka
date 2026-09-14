// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'lab_report.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

LabReport _$LabReportFromJson(Map<String, dynamic> json) => LabReport(
  id: json['id'] as String,
  patientId: json['patient_id'] as String,
  testName: json['test_name'] as String,
  fileName: json['file_name'] as String,
  contentType: json['content_type'] as String,
  byteSize: (json['byte_size'] as num).toInt(),
  uploadedByStaffId: json['uploaded_by_staff_id'] as String,
  createdAt: DateTime.parse(json['created_at'] as String),
  summary: json['summary'] as String?,
);

Map<String, dynamic> _$LabReportToJson(LabReport instance) => <String, dynamic>{
  'id': instance.id,
  'patient_id': instance.patientId,
  'test_name': instance.testName,
  'summary': instance.summary,
  'file_name': instance.fileName,
  'content_type': instance.contentType,
  'byte_size': instance.byteSize,
  'uploaded_by_staff_id': instance.uploadedByStaffId,
  'created_at': instance.createdAt.toIso8601String(),
};
