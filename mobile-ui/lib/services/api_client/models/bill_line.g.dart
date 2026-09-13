// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'bill_line.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

BillLine _$BillLineFromJson(Map<String, dynamic> json) => BillLine(
  id: json['id'] as String,
  source: BillLineSource.fromJson(json['source'] as String),
  description: json['description'] as String,
  quantity: (json['quantity'] as num).toDouble(),
  unitPrice: (json['unit_price'] as num).toDouble(),
  lineTotal: (json['line_total'] as num).toDouble(),
);

Map<String, dynamic> _$BillLineToJson(BillLine instance) => <String, dynamic>{
  'id': instance.id,
  'source': instance.source,
  'description': instance.description,
  'quantity': instance.quantity,
  'unit_price': instance.unitPrice,
  'line_total': instance.lineTotal,
};
