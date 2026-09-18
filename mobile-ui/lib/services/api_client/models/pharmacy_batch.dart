// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'pharmacy_batch.g.dart';

@JsonSerializable()
class PharmacyBatch {
  const PharmacyBatch({
    required this.id,
    required this.pharmacyItemId,
    required this.batchNumber,
    required this.quantityOnHand,
    required this.receivedAt,
    required this.updatedAt,
    this.reference,
    this.expiryDate,
    this.note,
  });
  
  factory PharmacyBatch.fromJson(Map<String, Object?> json) => _$PharmacyBatchFromJson(json);
  
  final String id;
  @JsonKey(name: 'pharmacy_item_id')
  final String pharmacyItemId;
  @JsonKey(name: 'batch_number')
  final int batchNumber;
  final String? reference;
  @JsonKey(name: 'expiry_date')
  final DateTime? expiryDate;
  @JsonKey(name: 'quantity_on_hand')
  final int quantityOnHand;
  final String? note;
  @JsonKey(name: 'received_at')
  final DateTime receivedAt;
  @JsonKey(name: 'updated_at')
  final DateTime updatedAt;

  Map<String, Object?> toJson() => _$PharmacyBatchToJson(this);
}
