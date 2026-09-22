// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'my_prescription.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

MyPrescription _$MyPrescriptionFromJson(Map<String, dynamic> json) =>
    MyPrescription(
      id: json['id'] as String,
      fileName: json['file_name'] as String,
      contentType: json['content_type'] as String,
      byteSize: (json['byte_size'] as num).toInt(),
      status: PrescriptionStatus.fromJson(json['status'] as String),
      createdAt: DateTime.parse(json['created_at'] as String),
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

Map<String, dynamic> _$MyPrescriptionToJson(MyPrescription instance) =>
    <String, dynamic>{
      'id': instance.id,
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
    };
