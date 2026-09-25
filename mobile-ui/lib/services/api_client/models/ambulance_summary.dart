// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'ambulance_eligibility_block_reason.dart';
import 'ambulance_status.dart';
import 'dispatch_summary.dart';

part 'ambulance_summary.g.dart';

@JsonSerializable()
class AmbulanceSummary {
  const AmbulanceSummary({
    required this.id,
    required this.registrationNumber,
    required this.status,
    required this.isDivertible,
    this.isActive,
    this.currentLatitude,
    this.currentLongitude,
    this.locationUpdatedAt,
    this.currentCrewCount,
    this.requiredCrewCount,
    this.isEligible,
    this.eligibilityBlockReasons,
    this.activeDispatchId,
    this.activeDispatch,
    this.distanceKm,
    this.driveMinutes,
    this.isStraightLineDistance,
  });
  
  factory AmbulanceSummary.fromJson(Map<String, Object?> json) => _$AmbulanceSummaryFromJson(json);
  
  final String id;
  @JsonKey(name: 'registration_number')
  final String registrationNumber;
  final AmbulanceStatus status;
  @JsonKey(name: 'is_active')
  final bool? isActive;
  @JsonKey(name: 'current_latitude')
  final double? currentLatitude;
  @JsonKey(name: 'current_longitude')
  final double? currentLongitude;
  @JsonKey(name: 'location_updated_at')
  final DateTime? locationUpdatedAt;
  @JsonKey(name: 'current_crew_count')
  final int? currentCrewCount;
  @JsonKey(name: 'required_crew_count')
  final int? requiredCrewCount;
  @JsonKey(name: 'is_eligible')
  final bool? isEligible;
  @JsonKey(name: 'eligibility_block_reasons')
  final List<AmbulanceEligibilityBlockReason>? eligibilityBlockReasons;
  @JsonKey(name: 'active_dispatch_id')
  final String? activeDispatchId;
  @JsonKey(name: 'active_dispatch')
  final DispatchSummary? activeDispatch;
  @JsonKey(name: 'is_divertible')
  final bool isDivertible;
  @JsonKey(name: 'distance_km')
  final double? distanceKm;
  @JsonKey(name: 'drive_minutes')
  final int? driveMinutes;
  @JsonKey(name: 'is_straight_line_distance')
  final bool? isStraightLineDistance;

  Map<String, Object?> toJson() => _$AmbulanceSummaryToJson(this);
}
