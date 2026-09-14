// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'expense_rate.dart';
import 'ward_type.dart';

part 'ward_rates.g.dart';

@JsonSerializable()
class WardRates {
  const WardRates({
    required this.wardType,
    required this.expenses,
  });
  
  factory WardRates.fromJson(Map<String, Object?> json) => _$WardRatesFromJson(json);
  
  @JsonKey(name: 'ward_type')
  final WardType wardType;
  final List<ExpenseRate> expenses;

  Map<String, Object?> toJson() => _$WardRatesToJson(this);
}
