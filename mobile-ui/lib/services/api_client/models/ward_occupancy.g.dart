// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'ward_occupancy.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

WardOccupancy _$WardOccupancyFromJson(Map<String, dynamic> json) =>
    WardOccupancy(
      wardId: json['ward_id'] as String,
      name: json['name'] as String,
      wardType: WardType.fromJson(json['ward_type'] as String),
      totalBeds: (json['total_beds'] as num).toInt(),
      occupiedBeds: (json['occupied_beds'] as num).toInt(),
      reservedBeds: (json['reserved_beds'] as num).toInt(),
      outOfServiceBeds: (json['out_of_service_beds'] as num).toInt(),
      patientsByCategory: Map<String, int>.from(
        json['patients_by_category'] as Map,
      ),
      incomingNext2h: (json['incoming_next_2h'] as num).toInt(),
    );

Map<String, dynamic> _$WardOccupancyToJson(WardOccupancy instance) =>
    <String, dynamic>{
      'ward_id': instance.wardId,
      'name': instance.name,
      'ward_type': instance.wardType,
      'total_beds': instance.totalBeds,
      'occupied_beds': instance.occupiedBeds,
      'reserved_beds': instance.reservedBeds,
      'out_of_service_beds': instance.outOfServiceBeds,
      'patients_by_category': instance.patientsByCategory,
      'incoming_next_2h': instance.incomingNext2h,
    };
