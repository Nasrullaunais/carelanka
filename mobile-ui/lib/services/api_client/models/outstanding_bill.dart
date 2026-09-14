// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'admission_category.dart';
import 'admission_status.dart';
import 'patient_summary.dart';

part 'outstanding_bill.g.dart';

@JsonSerializable()
class OutstandingBill {
  const OutstandingBill({
    required this.admissionId,
    required this.patient,
    required this.status,
    required this.admissionCategory,
    required this.wardName,
    required this.bedNumber,
    required this.settled,
    required this.estimatedTotal,
    required this.currency,
    this.admittedAt,
    this.billNumber,
    this.settledAt,
  });
  
  factory OutstandingBill.fromJson(Map<String, Object?> json) => _$OutstandingBillFromJson(json);
  
  @JsonKey(name: 'admission_id')
  final String admissionId;
  final PatientSummary patient;
  final AdmissionStatus status;
  @JsonKey(name: 'admission_category')
  final AdmissionCategory admissionCategory;
  @JsonKey(name: 'ward_name')
  final String wardName;
  @JsonKey(name: 'bed_number')
  final String bedNumber;
  @JsonKey(name: 'admitted_at')
  final DateTime? admittedAt;
  @JsonKey(name: 'bill_number')
  final String? billNumber;
  final bool settled;
  @JsonKey(name: 'settled_at')
  final DateTime? settledAt;
  @JsonKey(name: 'estimated_total')
  final double estimatedTotal;
  final String currency;

  Map<String, Object?> toJson() => _$OutstandingBillToJson(this);
}
