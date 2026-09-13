// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'admission_category.dart';

part 'admission_fee.g.dart';

@JsonSerializable()
class AdmissionFee {
  const AdmissionFee({
    required this.category,
    required this.amount,
  });
  
  factory AdmissionFee.fromJson(Map<String, Object?> json) => _$AdmissionFeeFromJson(json);
  
  final AdmissionCategory category;
  final double amount;

  Map<String, Object?> toJson() => _$AdmissionFeeToJson(this);
}
