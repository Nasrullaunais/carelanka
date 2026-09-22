// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'my_bill_line.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

MyBillLine _$MyBillLineFromJson(Map<String, dynamic> json) => MyBillLine(
  source: BillLineSource.fromJson(json['source'] as String),
  description: json['description'] as String,
  quantity: (json['quantity'] as num).toDouble(),
  unitPrice: (json['unit_price'] as num).toDouble(),
  lineTotal: (json['line_total'] as num).toDouble(),
);

Map<String, dynamic> _$MyBillLineToJson(MyBillLine instance) =>
    <String, dynamic>{
      'source': instance.source,
      'description': instance.description,
      'quantity': instance.quantity,
      'unit_price': instance.unitPrice,
      'line_total': instance.lineTotal,
    };
