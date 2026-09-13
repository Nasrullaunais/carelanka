// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'bed_availability.dart';
import 'bed_condition.dart';

part 'admission_bed.g.dart';

@JsonSerializable()
class AdmissionBed {
  const AdmissionBed({
    required this.id,
    required this.wardId,
    required this.wardName,
    required this.bedNumber,
    required this.hasIsolation,
    required this.condition,
    required this.availability,
    this.occupiedByAdmissionId,
    this.createdAt,
    this.updatedAt,
  });
  
  factory AdmissionBed.fromJson(Map<String, Object?> json) => _$AdmissionBedFromJson(json);
  
  final String id;
  @JsonKey(name: 'ward_id')
  final String wardId;
  @JsonKey(name: 'ward_name')
  final String wardName;
  @JsonKey(name: 'bed_number')
  final String bedNumber;
  @JsonKey(name: 'has_isolation')
  final bool hasIsolation;
  final BedCondition condition;
  final BedAvailability availability;
  @JsonKey(name: 'occupied_by_admission_id')
  final String? occupiedByAdmissionId;
  @JsonKey(name: 'created_at')
  final DateTime? createdAt;
  @JsonKey(name: 'updated_at')
  final DateTime? updatedAt;

  Map<String, Object?> toJson() => _$AdmissionBedToJson(this);
}
