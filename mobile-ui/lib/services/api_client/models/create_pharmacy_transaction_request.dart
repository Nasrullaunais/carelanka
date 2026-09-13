// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'pharmacy_transaction_type.dart';

part 'create_pharmacy_transaction_request.g.dart';

@JsonSerializable()
class CreatePharmacyTransactionRequest {
  const CreatePharmacyTransactionRequest({
    required this.type,
    required this.quantity,
    this.note,
  });
  
  factory CreatePharmacyTransactionRequest.fromJson(Map<String, Object?> json) => _$CreatePharmacyTransactionRequestFromJson(json);
  
  final PharmacyTransactionType type;
  final int quantity;
  final String? note;

  Map<String, Object?> toJson() => _$CreatePharmacyTransactionRequestToJson(this);
}
