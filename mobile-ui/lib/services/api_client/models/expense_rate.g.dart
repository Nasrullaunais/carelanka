// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'expense_rate.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

ExpenseRate _$ExpenseRateFromJson(Map<String, dynamic> json) => ExpenseRate(
  expenseKey: json['expense_key'] as String,
  amount: (json['amount'] as num).toDouble(),
);

Map<String, dynamic> _$ExpenseRateToJson(ExpenseRate instance) =>
    <String, dynamic>{
      'expense_key': instance.expenseKey,
      'amount': instance.amount,
    };
