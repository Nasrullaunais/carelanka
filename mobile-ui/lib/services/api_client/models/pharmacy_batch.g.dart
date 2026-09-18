// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'pharmacy_batch.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

PharmacyBatch _$PharmacyBatchFromJson(Map<String, dynamic> json) =>
    PharmacyBatch(
      id: json['id'] as String,
      pharmacyItemId: json['pharmacy_item_id'] as String,
      batchNumber: (json['batch_number'] as num).toInt(),
      quantityOnHand: (json['quantity_on_hand'] as num).toInt(),
      receivedAt: DateTime.parse(json['received_at'] as String),
      updatedAt: DateTime.parse(json['updated_at'] as String),
      reference: json['reference'] as String?,
      expiryDate: json['expiry_date'] == null
          ? null
          : DateTime.parse(json['expiry_date'] as String),
      note: json['note'] as String?,
    );

Map<String, dynamic> _$PharmacyBatchToJson(PharmacyBatch instance) =>
    <String, dynamic>{
      'id': instance.id,
      'pharmacy_item_id': instance.pharmacyItemId,
      'batch_number': instance.batchNumber,
      'reference': instance.reference,
      'expiry_date': instance.expiryDate?.toIso8601String(),
      'quantity_on_hand': instance.quantityOnHand,
      'note': instance.note,
      'received_at': instance.receivedAt.toIso8601String(),
      'updated_at': instance.updatedAt.toIso8601String(),
    };
