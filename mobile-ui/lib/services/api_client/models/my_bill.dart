// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'my_bill_line.dart';

part 'my_bill.g.dart';

@JsonSerializable()
class MyBill {
  const MyBill({
    required this.billNumber,
    required this.currency,
    required this.lines,
    required this.total,
    required this.isFinal,
    required this.settled,
    required this.updatedAt,
    this.admissionId,
    this.appointmentId,
    this.settledAt,
  });
  
  factory MyBill.fromJson(Map<String, Object?> json) => _$MyBillFromJson(json);
  
  @JsonKey(name: 'admission_id')
  final String? admissionId;
  @JsonKey(name: 'appointment_id')
  final String? appointmentId;
  @JsonKey(name: 'bill_number')
  final String billNumber;
  final String currency;
  final List<MyBillLine> lines;
  final double total;
  @JsonKey(name: 'is_final')
  final bool isFinal;
  final bool settled;
  @JsonKey(name: 'settled_at')
  final DateTime? settledAt;
  @JsonKey(name: 'updated_at')
  final DateTime updatedAt;

  Map<String, Object?> toJson() => _$MyBillToJson(this);
}
