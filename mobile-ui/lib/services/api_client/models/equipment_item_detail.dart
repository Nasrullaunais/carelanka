// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'equipment_status.dart';
import 'maintenance_schedule.dart';
import 'warning.dart';

part 'equipment_item_detail.g.dart';

@JsonSerializable()
class EquipmentItemDetail {
  const EquipmentItemDetail({
    required this.id,
    required this.name,
    required this.categoryId,
    required this.categoryName,
    required this.model,
    required this.manufacturer,
    required this.assetTag,
    required this.status,
    required this.purchaseDate,
    required this.createdAt,
    required this.updatedAt,
    required this.maintenanceHistory,
    required this.openWarnings,
    this.wardId,
    this.wardName,
    this.nextMaintenanceDue,
    this.serialNumber,
    this.assignedToAdmissionId,
  });
  
  factory EquipmentItemDetail.fromJson(Map<String, Object?> json) => _$EquipmentItemDetailFromJson(json);
  
  final String id;
  final String name;
  @JsonKey(name: 'category_id')
  final String categoryId;
  @JsonKey(name: 'category_name')
  final String categoryName;
  final String model;
  final String manufacturer;
  @JsonKey(name: 'asset_tag')
  final String assetTag;
  @JsonKey(name: 'ward_id')
  final String? wardId;
  @JsonKey(name: 'ward_name')
  final String? wardName;
  final EquipmentStatus status;
  @JsonKey(name: 'next_maintenance_due')
  final DateTime? nextMaintenanceDue;
  @JsonKey(name: 'purchase_date')
  final DateTime purchaseDate;
  @JsonKey(name: 'serial_number')
  final String? serialNumber;
  @JsonKey(name: 'assigned_to_admission_id')
  final String? assignedToAdmissionId;
  @JsonKey(name: 'created_at')
  final DateTime createdAt;
  @JsonKey(name: 'updated_at')
  final DateTime updatedAt;
  @JsonKey(name: 'maintenance_history')
  final List<MaintenanceSchedule> maintenanceHistory;
  @JsonKey(name: 'open_warnings')
  final List<Warning> openWarnings;

  Map<String, Object?> toJson() => _$EquipmentItemDetailToJson(this);
}
