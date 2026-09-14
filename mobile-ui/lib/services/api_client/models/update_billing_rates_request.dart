// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'admission_fee_update.dart';
import 'ward_expense_rate_update.dart';

part 'update_billing_rates_request.g.dart';

@JsonSerializable()
class UpdateBillingRatesRequest {
  const UpdateBillingRatesRequest({
    this.expenses,
    this.admissionFees,
  });
  
  factory UpdateBillingRatesRequest.fromJson(Map<String, Object?> json) => _$UpdateBillingRatesRequestFromJson(json);
  
  final List<WardExpenseRateUpdate>? expenses;
  @JsonKey(name: 'admission_fees')
  final List<AdmissionFeeUpdate>? admissionFees;

  Map<String, Object?> toJson() => _$UpdateBillingRatesRequestToJson(this);
}
