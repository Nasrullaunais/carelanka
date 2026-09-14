// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'create_pharmacy_item_request.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

CreatePharmacyItemRequest _$CreatePharmacyItemRequestFromJson(
  Map<String, dynamic> json,
) => CreatePharmacyItemRequest(
  name: json['name'] as String,
  categoryId: json['category_id'] as String,
  unit: json['unit'] as String,
  manufacturer: json['manufacturer'] as String?,
  batchNumber: json['batch_number'] as String?,
  expiryDate: json['expiry_date'] == null
      ? null
      : DateTime.parse(json['expiry_date'] as String),
  quantityOnHand: (json['quantity_on_hand'] as num?)?.toInt(),
  reorderThreshold: (json['reorder_threshold'] as num?)?.toInt(),
  unitPrice: (json['unit_price'] as num?)?.toDouble(),
);

Map<String, dynamic> _$CreatePharmacyItemRequestToJson(
  CreatePharmacyItemRequest instance,
) => <String, dynamic>{
  'name': instance.name,
  'category_id': instance.categoryId,
  'manufacturer': instance.manufacturer,
  'batch_number': instance.batchNumber,
  'expiry_date': instance.expiryDate?.toIso8601String(),
  'unit': instance.unit,
  'quantity_on_hand': instance.quantityOnHand,
  'reorder_threshold': instance.reorderThreshold,
  'unit_price': instance.unitPrice,
};
