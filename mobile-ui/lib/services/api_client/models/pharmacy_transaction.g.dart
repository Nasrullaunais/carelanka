// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'pharmacy_transaction.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

PharmacyTransaction _$PharmacyTransactionFromJson(Map<String, dynamic> json) =>
    PharmacyTransaction(
      id: json['id'] as String,
      pharmacyItemId: json['pharmacy_item_id'] as String,
      type: PharmacyTransactionType.fromJson(json['type'] as String),
      quantity: (json['quantity'] as num).toInt(),
      performedByStaffId: json['performed_by_staff_id'] as String,
      createdAt: DateTime.parse(json['created_at'] as String),
      note: json['note'] as String?,
    );

Map<String, dynamic> _$PharmacyTransactionToJson(
  PharmacyTransaction instance,
) => <String, dynamic>{
  'id': instance.id,
  'pharmacy_item_id': instance.pharmacyItemId,
  'type': instance.type,
  'quantity': instance.quantity,
  'performed_by_staff_id': instance.performedByStaffId,
  'note': instance.note,
  'created_at': instance.createdAt.toIso8601String(),
};
