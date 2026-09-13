// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'ward_type.dart';

part 'ward_occupancy.g.dart';

@JsonSerializable()
class WardOccupancy {
  const WardOccupancy({
    required this.wardId,
    required this.name,
    required this.wardType,
    required this.totalBeds,
    required this.occupiedBeds,
    required this.reservedBeds,
    required this.outOfServiceBeds,
    required this.patientsByCategory,
    required this.incomingNext2h,
  });
  
  factory WardOccupancy.fromJson(Map<String, Object?> json) => _$WardOccupancyFromJson(json);
  
  @JsonKey(name: 'ward_id')
  final String wardId;
  final String name;
  @JsonKey(name: 'ward_type')
  final WardType wardType;
  @JsonKey(name: 'total_beds')
  final int totalBeds;
  @JsonKey(name: 'occupied_beds')
  final int occupiedBeds;
  @JsonKey(name: 'reserved_beds')
  final int reservedBeds;
  @JsonKey(name: 'out_of_service_beds')
  final int outOfServiceBeds;
  @JsonKey(name: 'patients_by_category')
  final Map<String, int> patientsByCategory;
  @JsonKey(name: 'incoming_next_2h')
  final int incomingNext2h;

  Map<String, Object?> toJson() => _$WardOccupancyToJson(this);
}
