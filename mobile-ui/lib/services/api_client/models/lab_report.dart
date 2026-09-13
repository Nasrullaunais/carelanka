// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'lab_report.g.dart';

@JsonSerializable()
class LabReport {
  const LabReport({
    required this.id,
    required this.patientId,
    required this.testName,
    required this.fileName,
    required this.contentType,
    required this.byteSize,
    required this.uploadedByStaffId,
    required this.createdAt,
    this.summary,
  });
  
  factory LabReport.fromJson(Map<String, Object?> json) => _$LabReportFromJson(json);
  
  final String id;
  @JsonKey(name: 'patient_id')
  final String patientId;
  @JsonKey(name: 'test_name')
  final String testName;
  final String? summary;
  @JsonKey(name: 'file_name')
  final String fileName;
  @JsonKey(name: 'content_type')
  final String contentType;
  @JsonKey(name: 'byte_size')
  final int byteSize;
  @JsonKey(name: 'uploaded_by_staff_id')
  final String uploadedByStaffId;
  @JsonKey(name: 'created_at')
  final DateTime createdAt;

  Map<String, Object?> toJson() => _$LabReportToJson(this);
}
