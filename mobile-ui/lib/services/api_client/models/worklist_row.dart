// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'admission_category.dart';
import 'admission_source.dart';
import 'admission_urgency.dart';
import 'patient_summary.dart';
import 'worklist_status.dart';

part 'worklist_row.g.dart';

@JsonSerializable()
class WorklistRow {
  const WorklistRow({
    required this.id,
    required this.patient,
    required this.status,
    required this.requiresBed,
    required this.whenValue,
    this.source,
    this.admissionCategory,
    this.urgency,
    this.isInfectious,
    this.wardName,
    this.bedNumber,
  });
  
  factory WorklistRow.fromJson(Map<String, Object?> json) => _$WorklistRowFromJson(json);
  
  final String id;
  final PatientSummary patient;
  final WorklistStatus status;
  @JsonKey(name: 'requires_bed')
  final bool requiresBed;
  final AdmissionSource? source;
  @JsonKey(name: 'admission_category')
  final AdmissionCategory? admissionCategory;
  final AdmissionUrgency? urgency;
  @JsonKey(name: 'is_infectious')
  final bool? isInfectious;
  @JsonKey(name: 'ward_name')
  final String? wardName;
  @JsonKey(name: 'bed_number')
  final String? bedNumber;

  /// The name has been replaced because it contains a keyword. Original name: `when`.
  @JsonKey(name: 'when')
  final DateTime whenValue;

  Map<String, Object?> toJson() => _$WorklistRowToJson(this);
}
