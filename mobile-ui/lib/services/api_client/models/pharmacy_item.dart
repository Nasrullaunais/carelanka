// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'pharmacy_item.g.dart';

@JsonSerializable()
class PharmacyItem {
  const PharmacyItem({
    required this.id,
    required this.name,
    required this.categoryId,
    required this.categoryName,
    required this.unit,
    required this.quantityOnHand,
    required this.reorderThreshold,
    required this.isAvailable,
    required this.belowThreshold,
    required this.createdAt,
    required this.updatedAt,
    this.manufacturer,
    this.batchNumber,
    this.expiryDate,
    this.unitPrice,
  });
  
  factory PharmacyItem.fromJson(Map<String, Object?> json) => _$PharmacyItemFromJson(json);
  
  final String id;
  final String name;
  @JsonKey(name: 'category_id')
  final String categoryId;
  @JsonKey(name: 'category_name')
  final String categoryName;
  final String? manufacturer;
  @JsonKey(name: 'batch_number')
  final String? batchNumber;
  @JsonKey(name: 'expiry_date')
  final DateTime? expiryDate;
  final String unit;
  @JsonKey(name: 'quantity_on_hand')
  final int quantityOnHand;
  @JsonKey(name: 'reorder_threshold')
  final int reorderThreshold;
  @JsonKey(name: 'unit_price')
  final double? unitPrice;
  @JsonKey(name: 'is_available')
  final bool isAvailable;
  @JsonKey(name: 'below_threshold')
  final bool belowThreshold;
  @JsonKey(name: 'created_at')
  final DateTime createdAt;
  @JsonKey(name: 'updated_at')
  final DateTime updatedAt;

  Map<String, Object?> toJson() => _$PharmacyItemToJson(this);
}
