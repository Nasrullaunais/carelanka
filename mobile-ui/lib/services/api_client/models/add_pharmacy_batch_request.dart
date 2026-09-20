// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'add_pharmacy_batch_request.g.dart';

@JsonSerializable()
class AddPharmacyBatchRequest {
  const AddPharmacyBatchRequest({
    required this.quantity,
    this.expiryDate,
    this.reference,
    this.note,
  });
  
  factory AddPharmacyBatchRequest.fromJson(Map<String, Object?> json) => _$AddPharmacyBatchRequestFromJson(json);
  
  final int quantity;
  @JsonKey(name: 'expiry_date')
  final DateTime? expiryDate;
  final String? reference;
  final String? note;

  Map<String, Object?> toJson() => _$AddPharmacyBatchRequestToJson(this);
}
