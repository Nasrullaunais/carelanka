// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'create_pharmacy_transaction_request.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

CreatePharmacyTransactionRequest _$CreatePharmacyTransactionRequestFromJson(
  Map<String, dynamic> json,
) => CreatePharmacyTransactionRequest(
  type: PharmacyTransactionType.fromJson(json['type'] as String),
  quantity: (json['quantity'] as num).toInt(),
  note: json['note'] as String?,
);

Map<String, dynamic> _$CreatePharmacyTransactionRequestToJson(
  CreatePharmacyTransactionRequest instance,
) => <String, dynamic>{
  'type': instance.type,
  'quantity': instance.quantity,
  'note': instance.note,
};
