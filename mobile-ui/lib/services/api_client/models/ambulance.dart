// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'ambulance_status.dart';

part 'ambulance.g.dart';

@JsonSerializable()
class Ambulance {
  const Ambulance({
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
  });
  
  factory Ambulance.fromJson(Map<String, Object?> json) => _$AmbulanceFromJson(json);
  
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

  Map<String, Object?> toJson() => _$AmbulanceToJson(this);
}
