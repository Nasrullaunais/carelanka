// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'ward_rates.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

WardRates _$WardRatesFromJson(Map<String, dynamic> json) => WardRates(
  wardType: WardType.fromJson(json['ward_type'] as String),
  expenses: (json['expenses'] as List<dynamic>)
      .map((e) => ExpenseRate.fromJson(e as Map<String, dynamic>))
      .toList(),
);

Map<String, dynamic> _$WardRatesToJson(WardRates instance) => <String, dynamic>{
  'ward_type': instance.wardType,
  'expenses': instance.expenses,
};
