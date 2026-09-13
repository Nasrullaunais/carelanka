// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'ward_expense_rate_update.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

WardExpenseRateUpdate _$WardExpenseRateUpdateFromJson(
  Map<String, dynamic> json,
) => WardExpenseRateUpdate(
  wardType: WardType.fromJson(json['ward_type'] as String),
  expenseKey: json['expense_key'] as String,
  amount: (json['amount'] as num).toDouble(),
);

Map<String, dynamic> _$WardExpenseRateUpdateToJson(
  WardExpenseRateUpdate instance,
) => <String, dynamic>{
  'ward_type': instance.wardType,
  'expense_key': instance.expenseKey,
  'amount': instance.amount,
};
