// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'admission_fee_update.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

AdmissionFeeUpdate _$AdmissionFeeUpdateFromJson(Map<String, dynamic> json) =>
    AdmissionFeeUpdate(
      category: AdmissionCategory.fromJson(json['category'] as String),
      amount: (json['amount'] as num).toDouble(),
    );

Map<String, dynamic> _$AdmissionFeeUpdateToJson(AdmissionFeeUpdate instance) =>
    <String, dynamic>{'category': instance.category, 'amount': instance.amount};
