// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'prescription_status.dart';

part 'prescription.g.dart';

@JsonSerializable()
class Prescription {
  const Prescription({
    required this.id,
    required this.patientId,
    required this.patientCode,
    required this.patientName,
    required this.fileName,
    required this.contentType,
    required this.byteSize,
    required this.status,
    required this.createdAt,
    required this.updatedAt,
    this.note,
    this.tokenDate,
    this.tokenNumber,
    this.readyAt,
    this.deliveredAt,
    this.rejectionReason,
  });
  
  factory Prescription.fromJson(Map<String, Object?> json) => _$PrescriptionFromJson(json);
  
  final String id;
  @JsonKey(name: 'patient_id')
  final String patientId;
  @JsonKey(name: 'patient_code')
  final String patientCode;
  @JsonKey(name: 'patient_name')
  final String patientName;
  final String? note;
  @JsonKey(name: 'file_name')
  final String fileName;
  @JsonKey(name: 'content_type')
  final String contentType;
  @JsonKey(name: 'byte_size')
  final int byteSize;
  final PrescriptionStatus status;
  @JsonKey(name: 'token_date')
  final DateTime? tokenDate;
  @JsonKey(name: 'token_number')
  final int? tokenNumber;
  @JsonKey(name: 'ready_at')
  final DateTime? readyAt;
  @JsonKey(name: 'delivered_at')
  final DateTime? deliveredAt;
  @JsonKey(name: 'rejection_reason')
  final String? rejectionReason;
  @JsonKey(name: 'created_at')
  final DateTime createdAt;
  @JsonKey(name: 'updated_at')
  final DateTime updatedAt;

  Map<String, Object?> toJson() => _$PrescriptionToJson(this);
}
