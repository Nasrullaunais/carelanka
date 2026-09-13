// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'outstanding_bill.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

OutstandingBill _$OutstandingBillFromJson(Map<String, dynamic> json) =>
    OutstandingBill(
      admissionId: json['admission_id'] as String,
      patient: PatientSummary.fromJson(json['patient'] as Map<String, dynamic>),
      status: AdmissionStatus.fromJson(json['status'] as String),
      admissionCategory: AdmissionCategory.fromJson(
        json['admission_category'] as String,
      ),
      wardName: json['ward_name'] as String,
      bedNumber: json['bed_number'] as String,
      settled: json['settled'] as bool,
      estimatedTotal: (json['estimated_total'] as num).toDouble(),
      currency: json['currency'] as String,
      admittedAt: json['admitted_at'] == null
          ? null
          : DateTime.parse(json['admitted_at'] as String),
      billNumber: json['bill_number'] as String?,
      settledAt: json['settled_at'] == null
          ? null
          : DateTime.parse(json['settled_at'] as String),
    );

Map<String, dynamic> _$OutstandingBillToJson(OutstandingBill instance) =>
    <String, dynamic>{
      'admission_id': instance.admissionId,
      'patient': instance.patient,
      'status': instance.status,
      'admission_category': instance.admissionCategory,
      'ward_name': instance.wardName,
      'bed_number': instance.bedNumber,
      'admitted_at': instance.admittedAt?.toIso8601String(),
      'bill_number': instance.billNumber,
      'settled': instance.settled,
      'settled_at': instance.settledAt?.toIso8601String(),
      'estimated_total': instance.estimatedTotal,
      'currency': instance.currency,
    };
