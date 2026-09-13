// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'billing_rate_book.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

BillingRateBook _$BillingRateBookFromJson(Map<String, dynamic> json) =>
    BillingRateBook(
      wards: (json['wards'] as List<dynamic>)
          .map((e) => WardRates.fromJson(e as Map<String, dynamic>))
          .toList(),
      admissionFees: (json['admission_fees'] as List<dynamic>)
          .map((e) => AdmissionFee.fromJson(e as Map<String, dynamic>))
          .toList(),
      currency: json['currency'] as String,
    );

Map<String, dynamic> _$BillingRateBookToJson(BillingRateBook instance) =>
    <String, dynamic>{
      'wards': instance.wards,
      'admission_fees': instance.admissionFees,
      'currency': instance.currency,
    };
