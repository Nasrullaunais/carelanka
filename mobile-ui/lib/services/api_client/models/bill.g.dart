// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'bill.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

Bill _$BillFromJson(Map<String, dynamic> json) => Bill(
  id: json['id'] as String,
  admissionId: json['admission_id'] as String,
  billNumber: json['bill_number'] as String,
  currency: json['currency'] as String,
  lines: (json['lines'] as List<dynamic>)
      .map((e) => BillLine.fromJson(e as Map<String, dynamic>))
      .toList(),
  total: (json['total'] as num).toDouble(),
  settled: json['settled'] as bool,
  patient: PatientSummary.fromJson(json['patient'] as Map<String, dynamic>),
  createdAt: DateTime.parse(json['created_at'] as String),
  updatedAt: DateTime.parse(json['updated_at'] as String),
  raisedByStaffId: json['raised_by_staff_id'] as String?,
  raisedByStaffName: json['raised_by_staff_name'] as String?,
  settledAt: json['settled_at'] == null
      ? null
      : DateTime.parse(json['settled_at'] as String),
  settledByStaffId: json['settled_by_staff_id'] as String?,
  settledByStaffName: json['settled_by_staff_name'] as String?,
  settlementNote: json['settlement_note'] as String?,
);

Map<String, dynamic> _$BillToJson(Bill instance) => <String, dynamic>{
  'id': instance.id,
  'admission_id': instance.admissionId,
  'bill_number': instance.billNumber,
  'currency': instance.currency,
  'lines': instance.lines,
  'total': instance.total,
  'settled': instance.settled,
  'raised_by_staff_id': instance.raisedByStaffId,
  'raised_by_staff_name': instance.raisedByStaffName,
  'settled_at': instance.settledAt?.toIso8601String(),
  'settled_by_staff_id': instance.settledByStaffId,
  'settled_by_staff_name': instance.settledByStaffName,
  'settlement_note': instance.settlementNote,
  'patient': instance.patient,
  'created_at': instance.createdAt.toIso8601String(),
  'updated_at': instance.updatedAt.toIso8601String(),
};
