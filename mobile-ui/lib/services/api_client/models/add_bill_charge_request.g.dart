// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'add_bill_charge_request.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

AddBillChargeRequest _$AddBillChargeRequestFromJson(
  Map<String, dynamic> json,
) => AddBillChargeRequest(
  description: json['description'] as String,
  quantity: (json['quantity'] as num).toDouble(),
  unitPrice: (json['unit_price'] as num).toDouble(),
);

Map<String, dynamic> _$AddBillChargeRequestToJson(
  AddBillChargeRequest instance,
) => <String, dynamic>{
  'description': instance.description,
  'quantity': instance.quantity,
  'unit_price': instance.unitPrice,
};
