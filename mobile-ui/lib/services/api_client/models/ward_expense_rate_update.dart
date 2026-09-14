// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'ward_type.dart';

part 'ward_expense_rate_update.g.dart';

@JsonSerializable()
class WardExpenseRateUpdate {
  const WardExpenseRateUpdate({
    required this.wardType,
    required this.expenseKey,
    required this.amount,
  });
  
  factory WardExpenseRateUpdate.fromJson(Map<String, Object?> json) => _$WardExpenseRateUpdateFromJson(json);
  
  @JsonKey(name: 'ward_type')
  final WardType wardType;
  @JsonKey(name: 'expense_key')
  final String expenseKey;
  final double amount;

  Map<String, Object?> toJson() => _$WardExpenseRateUpdateToJson(this);
}
