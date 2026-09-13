// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'admission_category.dart';
import 'admission_source.dart';
import 'admission_status.dart';
import 'admission_urgency.dart';
import 'bed_assignment.dart';
import 'bill.dart';
import 'cancel_reason.dart';
import 'discharge.dart';
import 'patient_summary.dart';

part 'admission_detail.g.dart';

@JsonSerializable()
class AdmissionDetail {
  const AdmissionDetail({
    required this.id,
    required this.source,
    required this.admissionCategory,
    required this.urgency,
    required this.status,
    required this.detailsComplete,
    required this.requiresBed,
    required this.categorySetByStaffId,
    required this.categorySetAt,
    required this.isInfectious,
    required this.missingFields,
    required this.bedAssignments,
    this.patient,
    this.wardName,
    this.bedNumber,
    this.expectedArrival,
    this.admittedAt,
    this.dispatchId,
    this.categorySetByStaffName,
    this.reportedByUserId,
    this.dischargedAt,
    this.cancelReason,
    this.cancelNote,
    this.createdAt,
    this.updatedAt,
    this.discharge,
    this.bill,
  });
  
  factory AdmissionDetail.fromJson(Map<String, Object?> json) => _$AdmissionDetailFromJson(json);
  
  final String id;
  final PatientSummary? patient;
  final AdmissionSource source;
  @JsonKey(name: 'admission_category')
  final AdmissionCategory admissionCategory;
  final AdmissionUrgency urgency;
  final AdmissionStatus status;
  @JsonKey(name: 'details_complete')
  final bool detailsComplete;
  @JsonKey(name: 'requires_bed')
  final bool requiresBed;
  @JsonKey(name: 'ward_name')
  final String? wardName;
  @JsonKey(name: 'bed_number')
  final String? bedNumber;
  @JsonKey(name: 'expected_arrival')
  final DateTime? expectedArrival;
  @JsonKey(name: 'admitted_at')
  final DateTime? admittedAt;
  @JsonKey(name: 'dispatch_id')
  final String? dispatchId;
  @JsonKey(name: 'category_set_by_staff_id')
  final String categorySetByStaffId;
  @JsonKey(name: 'category_set_by_staff_name')
  final String? categorySetByStaffName;
  @JsonKey(name: 'category_set_at')
  final DateTime categorySetAt;
  @JsonKey(name: 'is_infectious')
  final bool isInfectious;
  @JsonKey(name: 'reported_by_user_id')
  final String? reportedByUserId;
  @JsonKey(name: 'missing_fields')
  final List<String> missingFields;
  @JsonKey(name: 'discharged_at')
  final DateTime? dischargedAt;
  @JsonKey(name: 'cancel_reason')
  final CancelReason? cancelReason;
  @JsonKey(name: 'cancel_note')
  final String? cancelNote;
  @JsonKey(name: 'created_at')
  final DateTime? createdAt;
  @JsonKey(name: 'updated_at')
  final DateTime? updatedAt;
  @JsonKey(name: 'bed_assignments')
  final List<BedAssignment> bedAssignments;
  final Discharge? discharge;
  final Bill? bill;

  Map<String, Object?> toJson() => _$AdmissionDetailToJson(this);
}
