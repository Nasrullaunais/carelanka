// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'bill_line_source.dart';

part 'my_bill_line.g.dart';

@JsonSerializable()
class MyBillLine {
  const MyBillLine({
    required this.source,
    required this.description,
    required this.quantity,
    required this.unitPrice,
    required this.lineTotal,
  });
  
  factory MyBillLine.fromJson(Map<String, Object?> json) => _$MyBillLineFromJson(json);
  
  final BillLineSource source;
  final String description;
  final double quantity;
  @JsonKey(name: 'unit_price')
  final double unitPrice;
  @JsonKey(name: 'line_total')
  final double lineTotal;

  Map<String, Object?> toJson() => _$MyBillLineToJson(this);
}
