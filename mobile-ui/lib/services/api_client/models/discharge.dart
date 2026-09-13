// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'assigned_by.dart';
import 'checklist_item.dart';

part 'discharge.g.dart';

@JsonSerializable()
class Discharge {
  const Discharge({
    required this.id,
    required this.admissionId,
    required this.flaggedBy,
    required this.flaggedAt,
    required this.checklist,
    required this.allMandatoryTicked,
    required this.createdAt,
    required this.updatedAt,
    this.confirmedByStaffId,
    this.confirmedByStaffName,
    this.confirmedAt,
    this.summaryNote,
  });
  
  factory Discharge.fromJson(Map<String, Object?> json) => _$DischargeFromJson(json);
  
  final String id;
  @JsonKey(name: 'admission_id')
  final String admissionId;
  @JsonKey(name: 'flagged_by')
  final AssignedBy flaggedBy;
  @JsonKey(name: 'flagged_at')
  final DateTime flaggedAt;
  final Map<String, ChecklistItem> checklist;
  @JsonKey(name: 'all_mandatory_ticked')
  final bool allMandatoryTicked;
  @JsonKey(name: 'confirmed_by_staff_id')
  final String? confirmedByStaffId;
  @JsonKey(name: 'confirmed_by_staff_name')
  final String? confirmedByStaffName;
  @JsonKey(name: 'confirmed_at')
  final DateTime? confirmedAt;
  @JsonKey(name: 'summary_note')
  final String? summaryNote;
  @JsonKey(name: 'created_at')
  final DateTime createdAt;
  @JsonKey(name: 'updated_at')
  final DateTime updatedAt;

  Map<String, Object?> toJson() => _$DischargeToJson(this);
}
