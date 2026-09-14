// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'admission_fee.dart';
import 'ward_rates.dart';

part 'billing_rate_book.g.dart';

@JsonSerializable()
class BillingRateBook {
  const BillingRateBook({
    required this.wards,
    required this.admissionFees,
    required this.currency,
  });
  
  factory BillingRateBook.fromJson(Map<String, Object?> json) => _$BillingRateBookFromJson(json);
  
  final List<WardRates> wards;
  @JsonKey(name: 'admission_fees')
  final List<AdmissionFee> admissionFees;
  final String currency;

  Map<String, Object?> toJson() => _$BillingRateBookToJson(this);
}
