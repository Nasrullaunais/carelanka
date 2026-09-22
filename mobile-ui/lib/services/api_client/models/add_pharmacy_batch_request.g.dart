// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'add_pharmacy_batch_request.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

AddPharmacyBatchRequest _$AddPharmacyBatchRequestFromJson(
  Map<String, dynamic> json,
) => AddPharmacyBatchRequest(
  quantity: (json['quantity'] as num).toInt(),
  expiryDate: json['expiry_date'] == null
      ? null
      : DateTime.parse(json['expiry_date'] as String),
  reference: json['reference'] as String?,
  note: json['note'] as String?,
);

Map<String, dynamic> _$AddPharmacyBatchRequestToJson(
  AddPharmacyBatchRequest instance,
) => <String, dynamic>{
  'quantity': instance.quantity,
  'expiry_date': instance.expiryDate?.toIso8601String(),
  'reference': instance.reference,
  'note': instance.note,
};
