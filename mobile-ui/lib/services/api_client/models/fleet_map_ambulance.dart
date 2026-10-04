// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'ambulance_eligibility_block_reason.dart';
import 'ambulance_status.dart';
import 'dispatch_status.dart';

part 'fleet_map_ambulance.g.dart';

@JsonSerializable()
class FleetMapAmbulance {
  const FleetMapAmbulance({
    this.id,
    this.registrationNumber,
    this.status,
    this.outOfServiceReason,
    this.latitude,
    this.longitude,
    this.locationUpdatedAt,
    this.locationIsStale,
    this.currentCrewCount,
    this.requiredCrewCount,
    this.isEligible,
    this.eligibilityBlockReasons,
    this.activeDispatchId,
    this.activeDispatchStatus,
    this.activeCallId,
  });
  
  factory FleetMapAmbulance.fromJson(Map<String, Object?> json) => _$FleetMapAmbulanceFromJson(json);
  
  final String? id;
  @JsonKey(name: 'registration_number')
  final String? registrationNumber;
  final AmbulanceStatus? status;
  @JsonKey(name: 'out_of_service_reason')
  final String? outOfServiceReason;
  final double? latitude;
  final double? longitude;
  @JsonKey(name: 'location_updated_at')
  final DateTime? locationUpdatedAt;
  @JsonKey(name: 'location_is_stale')
  final bool? locationIsStale;
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
  @JsonKey(name: 'active_dispatch_status')
  final DispatchStatus? activeDispatchStatus;
  @JsonKey(name: 'active_call_id')
  final String? activeCallId;

  Map<String, Object?> toJson() => _$FleetMapAmbulanceToJson(this);
}
