// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'bill_line.dart';
import 'patient_summary.dart';

part 'bill.g.dart';

@JsonSerializable()
class Bill {
  const Bill({
    required this.id,
    required this.billNumber,
    required this.currency,
    required this.lines,
    required this.total,
    required this.settled,
    required this.patient,
    required this.createdAt,
    required this.updatedAt,
    this.admissionId,
    this.appointmentId,
    this.raisedByStaffId,
    this.raisedByStaffName,
    this.settledAt,
    this.settledByStaffId,
    this.settledByStaffName,
    this.settlementNote,
  });
  
  factory Bill.fromJson(Map<String, Object?> json) => _$BillFromJson(json);
  
  final String id;
  @JsonKey(name: 'admission_id')
  final String? admissionId;
  @JsonKey(name: 'appointment_id')
  final String? appointmentId;
  @JsonKey(name: 'bill_number')
  final String billNumber;
  final String currency;
  final List<BillLine> lines;
  final double total;
  final bool settled;
  @JsonKey(name: 'raised_by_staff_id')
  final String? raisedByStaffId;
  @JsonKey(name: 'raised_by_staff_name')
  final String? raisedByStaffName;
  @JsonKey(name: 'settled_at')
  final DateTime? settledAt;
  @JsonKey(name: 'settled_by_staff_id')
  final String? settledByStaffId;
  @JsonKey(name: 'settled_by_staff_name')
  final String? settledByStaffName;
  @JsonKey(name: 'settlement_note')
  final String? settlementNote;
  final PatientSummary patient;
  @JsonKey(name: 'created_at')
  final DateTime createdAt;
  @JsonKey(name: 'updated_at')
  final DateTime updatedAt;

  Map<String, Object?> toJson() => _$BillToJson(this);
}
