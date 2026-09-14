// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'ambulance_crew_assignment.dart';
import 'ambulance_status.dart';
import 'dispatch_summary.dart';

part 'ambulance_detail.g.dart';

@JsonSerializable()
class AmbulanceDetail {
  const AmbulanceDetail({
    required this.id,
    required this.registrationNumber,
    required this.status,
    required this.isActive,
    required this.createdAt,
    required this.updatedAt,
    this.currentLatitude,
    this.currentLongitude,
    this.locationUpdatedAt,
    this.outOfServiceReason,
    this.activeDispatch,
    this.isDivertible,
    this.runsToday,
    this.currentCrew,
  });
  
  factory AmbulanceDetail.fromJson(Map<String, Object?> json) => _$AmbulanceDetailFromJson(json);
  
  final String id;
  @JsonKey(name: 'registration_number')
  final String registrationNumber;
  @JsonKey(name: 'current_latitude')
  final double? currentLatitude;
  @JsonKey(name: 'current_longitude')
  final double? currentLongitude;
  @JsonKey(name: 'location_updated_at')
  final DateTime? locationUpdatedAt;
  final AmbulanceStatus status;
  @JsonKey(name: 'out_of_service_reason')
  final String? outOfServiceReason;
  @JsonKey(name: 'is_active')
  final bool isActive;
  @JsonKey(name: 'created_at')
  final DateTime createdAt;
  @JsonKey(name: 'updated_at')
  final DateTime updatedAt;
  @JsonKey(name: 'active_dispatch')
  final DispatchSummary? activeDispatch;
  @JsonKey(name: 'is_divertible')
  final bool? isDivertible;
  @JsonKey(name: 'runs_today')
  final int? runsToday;
  @JsonKey(name: 'current_crew')
  final List<AmbulanceCrewAssignment>? currentCrew;

  Map<String, Object?> toJson() => _$AmbulanceDetailToJson(this);
}
