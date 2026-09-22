// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'pharmacy_item.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

PharmacyItem _$PharmacyItemFromJson(Map<String, dynamic> json) => PharmacyItem(
  id: json['id'] as String,
  name: json['name'] as String,
  categoryId: json['category_id'] as String,
  categoryName: json['category_name'] as String,
  unit: json['unit'] as String,
  batchCount: (json['batch_count'] as num).toInt(),
  quantityOnHand: (json['quantity_on_hand'] as num).toInt(),
  reorderThreshold: (json['reorder_threshold'] as num).toInt(),
  isAvailable: json['is_available'] as bool,
  belowThreshold: json['below_threshold'] as bool,
  createdAt: DateTime.parse(json['created_at'] as String),
  updatedAt: DateTime.parse(json['updated_at'] as String),
  manufacturer: json['manufacturer'] as String?,
  earliestExpiry: json['earliest_expiry'] == null
      ? null
      : DateTime.parse(json['earliest_expiry'] as String),
  unitPrice: (json['unit_price'] as num?)?.toDouble(),
);

Map<String, dynamic> _$PharmacyItemToJson(PharmacyItem instance) =>
    <String, dynamic>{
      'id': instance.id,
      'name': instance.name,
      'category_id': instance.categoryId,
      'category_name': instance.categoryName,
      'manufacturer': instance.manufacturer,
      'unit': instance.unit,
      'batch_count': instance.batchCount,
      'earliest_expiry': instance.earliestExpiry?.toIso8601String(),
      'quantity_on_hand': instance.quantityOnHand,
      'reorder_threshold': instance.reorderThreshold,
      'unit_price': instance.unitPrice,
      'is_available': instance.isAvailable,
      'below_threshold': instance.belowThreshold,
      'created_at': instance.createdAt.toIso8601String(),
      'updated_at': instance.updatedAt.toIso8601String(),
    };
