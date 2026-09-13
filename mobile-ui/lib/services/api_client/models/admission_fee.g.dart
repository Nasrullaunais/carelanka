// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'admission_fee.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

AdmissionFee _$AdmissionFeeFromJson(Map<String, dynamic> json) => AdmissionFee(
  category: AdmissionCategory.fromJson(json['category'] as String),
  amount: (json['amount'] as num).toDouble(),
);

Map<String, dynamic> _$AdmissionFeeToJson(AdmissionFee instance) =>
    <String, dynamic>{'category': instance.category, 'amount': instance.amount};
