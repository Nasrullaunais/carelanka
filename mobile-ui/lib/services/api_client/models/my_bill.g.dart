// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'my_bill.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

MyBill _$MyBillFromJson(Map<String, dynamic> json) => MyBill(
  admissionId: json['admission_id'] as String,
  billNumber: json['bill_number'] as String,
  currency: json['currency'] as String,
  lines: (json['lines'] as List<dynamic>)
      .map((e) => MyBillLine.fromJson(e as Map<String, dynamic>))
      .toList(),
  total: (json['total'] as num).toDouble(),
  isFinal: json['is_final'] as bool,
  settled: json['settled'] as bool,
  updatedAt: DateTime.parse(json['updated_at'] as String),
  settledAt: json['settled_at'] == null
      ? null
      : DateTime.parse(json['settled_at'] as String),
);

Map<String, dynamic> _$MyBillToJson(MyBill instance) => <String, dynamic>{
  'admission_id': instance.admissionId,
  'bill_number': instance.billNumber,
  'currency': instance.currency,
  'lines': instance.lines,
  'total': instance.total,
  'is_final': instance.isFinal,
  'settled': instance.settled,
  'settled_at': instance.settledAt?.toIso8601String(),
  'updated_at': instance.updatedAt.toIso8601String(),
};
