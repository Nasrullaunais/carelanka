// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'create_pharmacy_item_request.g.dart';

@JsonSerializable()
class CreatePharmacyItemRequest {
  const CreatePharmacyItemRequest({
    required this.name,
    required this.categoryId,
    required this.unit,
    this.manufacturer,
    this.batchNumber,
    this.expiryDate,
    this.quantityOnHand,
    this.reorderThreshold,
    this.unitPrice,
  });
  
  factory CreatePharmacyItemRequest.fromJson(Map<String, Object?> json) => _$CreatePharmacyItemRequestFromJson(json);
  
  final String name;
  @JsonKey(name: 'category_id')
  final String categoryId;
  final String? manufacturer;
  @JsonKey(name: 'batch_number')
  final String? batchNumber;
  @JsonKey(name: 'expiry_date')
  final DateTime? expiryDate;
  final String unit;
  @JsonKey(name: 'quantity_on_hand')
  final int? quantityOnHand;
  @JsonKey(name: 'reorder_threshold')
  final int? reorderThreshold;
  @JsonKey(name: 'unit_price')
  final double? unitPrice;

  Map<String, Object?> toJson() => _$CreatePharmacyItemRequestToJson(this);
}
