// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'pharmacy_transaction_type.dart';

part 'pharmacy_transaction.g.dart';

@JsonSerializable()
class PharmacyTransaction {
  const PharmacyTransaction({
    required this.id,
    required this.pharmacyItemId,
    required this.type,
    required this.quantity,
    required this.performedByStaffId,
    required this.createdAt,
    this.note,
  });
  
  factory PharmacyTransaction.fromJson(Map<String, Object?> json) => _$PharmacyTransactionFromJson(json);
  
  final String id;
  @JsonKey(name: 'pharmacy_item_id')
  final String pharmacyItemId;
  final PharmacyTransactionType type;
  final int quantity;
  @JsonKey(name: 'performed_by_staff_id')
  final String performedByStaffId;
  final String? note;
  @JsonKey(name: 'created_at')
  final DateTime createdAt;

  Map<String, Object?> toJson() => _$PharmacyTransactionToJson(this);
}
