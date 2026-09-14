// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'expense_rate.g.dart';

@JsonSerializable()
class ExpenseRate {
  const ExpenseRate({
    required this.expenseKey,
    required this.amount,
  });
  
  factory ExpenseRate.fromJson(Map<String, Object?> json) => _$ExpenseRateFromJson(json);
  
  @JsonKey(name: 'expense_key')
  final String expenseKey;
  final double amount;

  Map<String, Object?> toJson() => _$ExpenseRateToJson(this);
}
