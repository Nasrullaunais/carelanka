// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'admission_category.dart';

part 'admission_fee_update.g.dart';

@JsonSerializable()
class AdmissionFeeUpdate {
  const AdmissionFeeUpdate({
    required this.category,
    required this.amount,
  });
  
  factory AdmissionFeeUpdate.fromJson(Map<String, Object?> json) => _$AdmissionFeeUpdateFromJson(json);
  
  final AdmissionCategory category;
  final double amount;

  Map<String, Object?> toJson() => _$AdmissionFeeUpdateToJson(this);
}
