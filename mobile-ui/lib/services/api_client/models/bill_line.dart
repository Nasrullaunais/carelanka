// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'bill_line_source.dart';

part 'bill_line.g.dart';

@JsonSerializable()
class BillLine {
  const BillLine({
    required this.id,
    required this.source,
    required this.description,
    required this.quantity,
    required this.unitPrice,
    required this.lineTotal,
  });
  
  factory BillLine.fromJson(Map<String, Object?> json) => _$BillLineFromJson(json);
  
  final String id;
  final BillLineSource source;
  final String description;
  final double quantity;
  @JsonKey(name: 'unit_price')
  final double unitPrice;
  @JsonKey(name: 'line_total')
  final double lineTotal;

  Map<String, Object?> toJson() => _$BillLineToJson(this);
}
