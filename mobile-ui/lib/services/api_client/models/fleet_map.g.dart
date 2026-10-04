// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'fleet_map.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

FleetMap _$FleetMapFromJson(Map<String, dynamic> json) => FleetMap(
  ambulances: (json['ambulances'] as List<dynamic>?)
      ?.map((e) => FleetMapAmbulance.fromJson(e as Map<String, dynamic>))
      .toList(),
  calls: (json['calls'] as List<dynamic>?)
      ?.map((e) => FleetMapCall.fromJson(e as Map<String, dynamic>))
      .toList(),
  locationMaxAgeMinutes: (json['location_max_age_minutes'] as num?)?.toInt(),
  generatedAt: json['generated_at'] == null
      ? null
      : DateTime.parse(json['generated_at'] as String),
);

Map<String, dynamic> _$FleetMapToJson(FleetMap instance) => <String, dynamic>{
  'ambulances': instance.ambulances,
  'calls': instance.calls,
  'location_max_age_minutes': instance.locationMaxAgeMinutes,
  'generated_at': instance.generatedAt?.toIso8601String(),
};
