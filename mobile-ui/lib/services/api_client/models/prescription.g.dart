// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'prescription.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

Prescription _$PrescriptionFromJson(Map<String, dynamic> json) => Prescription(
  id: json['id'] as String,
  patientId: json['patient_id'] as String,
  patientCode: json['patient_code'] as String,
  patientName: json['patient_name'] as String,
  fileName: json['file_name'] as String,
  contentType: json['content_type'] as String,
  byteSize: (json['byte_size'] as num).toInt(),
  status: PrescriptionStatus.fromJson(json['status'] as String),
  createdAt: DateTime.parse(json['created_at'] as String),
  updatedAt: DateTime.parse(json['updated_at'] as String),
  note: json['note'] as String?,
  tokenDate: json['token_date'] == null
      ? null
      : DateTime.parse(json['token_date'] as String),
  tokenNumber: (json['token_number'] as num?)?.toInt(),
  readyAt: json['ready_at'] == null
      ? null
      : DateTime.parse(json['ready_at'] as String),
  deliveredAt: json['delivered_at'] == null
      ? null
      : DateTime.parse(json['delivered_at'] as String),
  rejectionReason: json['rejection_reason'] as String?,
);

Map<String, dynamic> _$PrescriptionToJson(Prescription instance) =>
    <String, dynamic>{
      'id': instance.id,
      'patient_id': instance.patientId,
      'patient_code': instance.patientCode,
      'patient_name': instance.patientName,
      'note': instance.note,
      'file_name': instance.fileName,
      'content_type': instance.contentType,
      'byte_size': instance.byteSize,
      'status': instance.status,
      'token_date': instance.tokenDate?.toIso8601String(),
      'token_number': instance.tokenNumber,
      'ready_at': instance.readyAt?.toIso8601String(),
      'delivered_at': instance.deliveredAt?.toIso8601String(),
      'rejection_reason': instance.rejectionReason,
      'created_at': instance.createdAt.toIso8601String(),
      'updated_at': instance.updatedAt.toIso8601String(),
    };
