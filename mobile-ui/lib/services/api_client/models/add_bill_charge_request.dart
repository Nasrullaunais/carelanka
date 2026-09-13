// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'add_bill_charge_request.g.dart';

@JsonSerializable()
class AddBillChargeRequest {
  const AddBillChargeRequest({
    required this.description,
    required this.quantity,
    required this.unitPrice,
  });
  
  factory AddBillChargeRequest.fromJson(Map<String, Object?> json) => _$AddBillChargeRequestFromJson(json);
  
  final String description;
  final double quantity;
  @JsonKey(name: 'unit_price')
  final double unitPrice;

  Map<String, Object?> toJson() => _$AddBillChargeRequestToJson(this);
}
