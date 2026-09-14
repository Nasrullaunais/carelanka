// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'update_billing_rates_request.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

UpdateBillingRatesRequest _$UpdateBillingRatesRequestFromJson(
  Map<String, dynamic> json,
) => UpdateBillingRatesRequest(
  expenses: (json['expenses'] as List<dynamic>?)
      ?.map((e) => WardExpenseRateUpdate.fromJson(e as Map<String, dynamic>))
      .toList(),
  admissionFees: (json['admission_fees'] as List<dynamic>?)
      ?.map((e) => AdmissionFeeUpdate.fromJson(e as Map<String, dynamic>))
      .toList(),
);

Map<String, dynamic> _$UpdateBillingRatesRequestToJson(
  UpdateBillingRatesRequest instance,
) => <String, dynamic>{
  'expenses': instance.expenses,
  'admission_fees': instance.admissionFees,
};
