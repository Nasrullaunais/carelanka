// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'report_ambulance_location_request.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

ReportAmbulanceLocationRequest _$ReportAmbulanceLocationRequestFromJson(
  Map<String, dynamic> json,
) => ReportAmbulanceLocationRequest(
  latitude: (json['latitude'] as num?)?.toDouble(),
  longitude: (json['longitude'] as num?)?.toDouble(),
);

Map<String, dynamic> _$ReportAmbulanceLocationRequestToJson(
  ReportAmbulanceLocationRequest instance,
) => <String, dynamic>{
  'latitude': instance.latitude,
  'longitude': instance.longitude,
};
